#include <WiFi.h>
#include <TFT_eSPI.h>
#include <SPI.h>
#include "HX711.h"
#include <SD.h>

// --------------- CONFIG ---------------
#define SIMULATE         false
#define UPDATE_MS        1000
#define USE_EMA          true
#define EMA_ALPHA        0.25f
#define ALWAYS_SEND_ZERO true

#define WIFI_SSID "12345"
#define WIFI_PASS "88888888"
#define SERVER_IP "10.104.239.1"
#define SERVER_PORT 8080
#define TCP_RETRY_MS 5000

#define SD_CS 17

#define NO_LOAD_THRESHOLD   5.0f
#define NOLOAD_RESET_EMA_MS 4000

// Kích thước mặt đế (cm)
const float DECK_W_CM = 60.0f;
const float DECK_H_CM = 60.0f;
const float HALF_W = DECK_W_CM / 2.0f;
const float HALF_H = DECK_H_CM / 2.0f;

// Khu vực vẽ
const int PLOT_X0 = 20;
const int PLOT_Y0 = 210;
const int PLOT_W  = 280;
const int PLOT_H  = 200;

// HX711 pins (giữ wiring, gán lại ý nghĩa):
// F1 = Top-Left, F2 = Top-Right, F3 = Bottom-Right, F4 = Bottom-Left
const int HX_SCK1 = 4;
const int HX_DT1  = 16; // F1 TL
const int HX_SCK2 = 15;
const int HX_DT2  = 13; // F2 TR
const int HX_DT3  = 27; // F3 BR
const int HX_SCK3 = 14;
const int HX_DT4  = 25; // F4 BL
const int HX_SCK4 = 26;

// HX711 objects: index 0..3 => F1,F2,F3,F4
HX711 hx1, hx2, hx3, hx4;
float scaleFactor[4] = {47.255798,50.090199,46.726501,46.205700};
long offsetRaw[4] = { 0, 0, 0, 0 };

TFT_eSPI tft = TFT_eSPI();

// Trail buffer
#define MAX_POINTS 500
float trailX[MAX_POINTS], trailY[MAX_POINTS];
int   trailCount = 0;

// EMA
float emaX = 0, emaY = 0;
bool  emaInit = false;
unsigned long lastLoadTime = 0;

// WiFi / TCP
WiFiClient client;
unsigned long lastTcpAttempt = 0;

// Timing
unsigned long lastUpdate = 0;

// --------------- COP (F1 TL, F2 TR, F3 BR, F4 BL) ---------------
static void computeCOP(float F1, float F2, float F3, float F4, float Ftot,
                       float &outXcm, float &outYcm) {
  if (Ftot <= 0) { outXcm = 0; outYcm = 0; return; }
  // Right side = F2 + F3; Left side = F1 + F4
  float X_norm = ((F2 + F3) - (F1 + F4)) / Ftot;
  // Top side = F1 + F2; Bottom side = F4 + F3
  float Y_norm = ((F1 + F2) - (F4 + F3)) / Ftot;
  outXcm = X_norm * HALF_W;
  outYcm = Y_norm * HALF_H;
  // Nếu muốn đảo chiều Y: đổi dấu Y_norm trước khi nhân.
}

// ---------------- Utility ----------------
int mapXcmToPixel(float x_cm) {
  float norm = (x_cm + HALF_W) / DECK_W_CM;
  return PLOT_X0 + (int)(norm * PLOT_W + 0.5f);
}
int mapYcmToPixel(float y_cm) {
  float norm = (y_cm + HALF_H) / DECK_H_CM;
  return PLOT_Y0 - (int)(norm * PLOT_H + 0.5f);
}

long readRawAverage(HX711 &h, int times) {
  long s = 0;
  for (int i=0;i<times;i++) {
    while (!h.is_ready()) delay(1);
    s += h.read();
  }
  return s / times;
}

void drawStaticAxes() {
  tft.fillRect(PLOT_X0-1, PLOT_Y0-PLOT_H-1, PLOT_W+2, PLOT_H+2, TFT_WHITE);
  tft.fillRect(PLOT_X0,   PLOT_Y0-PLOT_H,   PLOT_W,   PLOT_H,   TFT_BLACK);
  tft.drawRect(PLOT_X0, PLOT_Y0-PLOT_H, PLOT_W, PLOT_H, TFT_WHITE);
  int cx = mapXcmToPixel(0);
  int cy = mapYcmToPixel(0);
  tft.drawLine(PLOT_X0, cy, PLOT_X0+PLOT_W, cy, TFT_DARKGREY);
  tft.drawLine(cx, PLOT_Y0-PLOT_H, cx, PLOT_Y0, TFT_DARKGREY);

  int tickCm = 5;
  tft.setTextSize(1);
  for (int x = -(int)HALF_W; x <= (int)HALF_W; x += tickCm) {
    int px = mapXcmToPixel((float)x);
    tft.drawLine(px, PLOT_Y0-PLOT_H, px, PLOT_Y0-PLOT_H+4, TFT_WHITE);
    tft.drawLine(px, PLOT_Y0-4, px, PLOT_Y0, TFT_WHITE);
    if (x % 10 == 0) {
      tft.setTextColor(TFT_WHITE, TFT_BLACK);
      tft.setCursor(px-8, PLOT_Y0+4);
      tft.print(x);
    }
  }
  for (int y = -(int)HALF_H; y <= (int)HALF_H; y += tickCm) {
    int py = mapYcmToPixel((float)y);
    tft.drawLine(PLOT_X0, py, PLOT_X0+4, py, TFT_WHITE);
    tft.drawLine(PLOT_X0+PLOT_W-4, py, PLOT_X0+PLOT_W, py, TFT_WHITE);
    if (y % 10 == 0) {
      tft.setTextColor(TFT_WHITE, TFT_BLACK);
      tft.setCursor(2, py-4);
      tft.print(y);
    }
  }
}

void drawTitle() {
  tft.setTextColor(TFT_YELLOW, TFT_BLACK);
  tft.setTextSize(2);
  tft.setCursor(10, 10);
  tft.print("COP Scatter");
  tft.setTextSize(1);
  tft.setCursor(10, 30);
  tft.print("+/-30 cm");
}

void clearPlot() {
  trailCount = 0;
  emaInit = false;
  drawStaticAxes();
  drawTitle();
}

void redrawTrail(float curX, float curY) {
  tft.fillRect(PLOT_X0, PLOT_Y0-PLOT_H, PLOT_W, PLOT_H, TFT_BLACK);
  tft.drawRect(PLOT_X0, PLOT_Y0-PLOT_H, PLOT_W, PLOT_H, TFT_WHITE);
  int cx = mapXcmToPixel(0);
  int cy = mapYcmToPixel(0);
  tft.drawLine(PLOT_X0, cy, PLOT_X0+PLOT_W, cy, TFT_DARKGREY);
  tft.drawLine(cx, PLOT_Y0-PLOT_H, cx, PLOT_Y0, TFT_DARKGREY);

  int tickCm = 5;
  for (int x = -(int)HALF_W; x <= (int)HALF_W; x += tickCm) {
    int px = mapXcmToPixel(x);
    tft.drawLine(px, PLOT_Y0-PLOT_H, px, PLOT_Y0-PLOT_H+4, TFT_WHITE);
    tft.drawLine(px, PLOT_Y0-4, px, PLOT_Y0, TFT_WHITE);
  }
  for (int y = -(int)HALF_H; y <= (int)HALF_H; y += tickCm) {
    int py = mapYcmToPixel(y);
    tft.drawLine(PLOT_X0, py, PLOT_X0+4, py, TFT_WHITE);
    tft.drawLine(PLOT_X0+PLOT_W-4, py, PLOT_X0+PLOT_W, py, TFT_WHITE);
  }
  for (int i=0;i<trailCount;i++) {
    int px = mapXcmToPixel(trailX[i]);
    int py = mapYcmToPixel(trailY[i]);
    tft.fillCircle(px, py, 2, TFT_WHITE);
  }
  int cpx = mapXcmToPixel(curX);
  int cpy = mapYcmToPixel(curY);
  tft.fillCircle(cpx, cpy, 3, TFT_RED);
}

void addPoint(float x_cm, float y_cm) {
  if (trailCount >= MAX_POINTS) {
    for (int i=1;i<MAX_POINTS;i++) {
      trailX[i-1] = trailX[i];
      trailY[i-1] = trailY[i];
    }
    trailCount = MAX_POINTS - 1;
  }
  trailX[trailCount] = x_cm;
  trailY[trailCount] = y_cm;
  trailCount++;
  redrawTrail(x_cm, y_cm);
}

float readForce(HX711 &h, int idx) {
  long raw = 0;
  const int n = 8;
  for (int i=0;i<n;i++) {
    while (!h.is_ready()) delay(1);
    raw += h.read();
  }
  raw /= n;
  float val = (raw - offsetRaw[idx]) / scaleFactor[idx];
  if (val < 0) val = 0;
  return val;
}

// -------------- WiFi / TCP --------------
void ensureWifi() {
  if (WiFi.status() == WL_CONNECTED) return;
  WiFi.mode(WIFI_STA);
  WiFi.begin(WIFI_SSID, WIFI_PASS);
  Serial.print("WiFi connecting");
  int tries = 0;
  while (WiFi.status() != WL_CONNECTED && tries < 60) {
    delay(500);
    Serial.print(".");
    tries++;
  }
  Serial.println();
  if (WiFi.status() == WL_CONNECTED) {
    Serial.print("WiFi OK: "); Serial.println(WiFi.localIP());
  } else {
    Serial.println("WiFi FAIL");
  }
}

void ensureTcp() {
  if (client.connected()) return;
  unsigned long now = millis();
  if (now - lastTcpAttempt < TCP_RETRY_MS) return;
  lastTcpAttempt = now;
  Serial.print("TCP connect ");
  Serial.print(SERVER_IP); Serial.print(":"); Serial.println(SERVER_PORT);
  if (client.connect(SERVER_IP, SERVER_PORT)) {
    Serial.println("TCP connected");
    client.println("HELLO");
  } else {
    Serial.println("TCP failed");
  }
}

void handleIncomingCommands() {
  if (!client.connected()) return;
  while (client.available()) {
    String line = client.readStringUntil('\n');
    line.trim();
    if (line.equalsIgnoreCase("RESET") || line.equalsIgnoreCase("RESETALL")) {
      Serial.println("[CMD] RESET");
      clearPlot();
    } else if (line.equalsIgnoreCase("ZERO")) {
      Serial.println("[CMD] ZERO");
      // Cập nhật lại offset (để trống tải trước khi bấm trên PC)
      offsetRaw[0] = readRawAverage(hx1, 12);
      offsetRaw[1] = readRawAverage(hx2, 12);
      offsetRaw[2] = readRawAverage(hx3, 12);
      offsetRaw[3] = readRawAverage(hx4, 12);
      emaInit = false;
      Serial.println("ZERO done");
    }
  }
}

// Gửi đủ F1,F2,F3,F4,X,Y
void sendExtended(float F1, float F2, float F3, float F4, float X_cm, float Y_cm) {
  if (!client.connected()) return;
  client.printf("%.1f,%.1f,%.1f,%.1f,%.2f,%.2f\n", F1,F2,F3,F4,X_cm,Y_cm);
}

// -------------- Setup / Loop --------------
void setup() {
  Serial.begin(115200);
  delay(200);

  if (!SIMULATE) {
    hx1.begin(HX_DT1, HX_SCK1); // F1 TL
    hx2.begin(HX_DT2, HX_SCK2); // F2 TR
    hx3.begin(HX_DT3, HX_SCK3); // F3 BR
    hx4.begin(HX_DT4, HX_SCK4); // F4 BL
    offsetRaw[0] = readRawAverage(hx1, 15);
    offsetRaw[1] = readRawAverage(hx2, 15);
    offsetRaw[2] = readRawAverage(hx3, 15);
    offsetRaw[3] = readRawAverage(hx4, 15);
    Serial.println("Offsets (F1 F2 F3 F4):");
    Serial.printf("%ld %ld %ld %ld\n", offsetRaw[0],offsetRaw[1],offsetRaw[2],offsetRaw[3]);
  }

  tft.init();
  tft.setRotation(1);
  tft.fillScreen(TFT_BLACK);
  drawStaticAxes();
  drawTitle();

  randomSeed(analogRead(34));
  ensureWifi();
}

void loop() {
  ensureWifi();
  ensureTcp();
  handleIncomingCommands();

  unsigned long now = millis();
  if (now - lastUpdate < UPDATE_MS) return;
  lastUpdate = now;

  float F1,F2,F3,F4;
  if (SIMULATE) {
    F1 = random(700,1200)/10.0f;
    F2 = random(800,2200)/10.0f;
    F3 = random(300,4200)/10.0f;
    F4 = random(800,5200)/10.0f;
  } else {
    F1 = readForce(hx1,0);
    F2 = readForce(hx2,1);
    F3 = readForce(hx3,2);
    F4 = readForce(hx4,3);
  }

  float Ftot = F1 + F2 + F3 + F4;
  bool noLoad = (Ftot < NO_LOAD_THRESHOLD);

  float X_raw = 0, Y_raw = 0;
  if (!noLoad) {
    computeCOP(F1, F2, F3, F4, Ftot, X_raw, Y_raw);
    lastLoadTime = now;
  } else {
    if (now - lastLoadTime > NOLOAD_RESET_EMA_MS) {
      emaInit = false;
    }
  }

  if (!noLoad && USE_EMA) {
    if (!emaInit) { emaX = X_raw; emaY = Y_raw; emaInit = true; }
    else {
      emaX = emaX + EMA_ALPHA * (X_raw - emaX);
      emaY = emaY + EMA_ALPHA * (Y_raw - emaY);
    }
  }

  float X_cm = noLoad ? 0.0f : (USE_EMA ? emaX : X_raw);
  float Y_cm = noLoad ? 0.0f : (USE_EMA ? emaY : Y_raw);

 Serial.printf("F1..F4: %.1f,%.1f,%.1f,%.1f | Sum=%.1f | %s | COP=%.2f,%.2f\n",
                F1,F2,F3,F4,Ftot, noLoad ? "NOLOAD" : "LOAD", X_cm,Y_cm);


  if (!noLoad || (noLoad && ALWAYS_SEND_ZERO)) {
    addPoint(X_cm, Y_cm);
    sendExtended(F1,F2,F3,F4,X_cm,Y_cm);
  }
}