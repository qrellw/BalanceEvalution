using System;
using Microsoft.Data.SqlClient;

namespace BalanceApp
{
    internal static class DatabaseHelper
    {
        // Candidate connection strings to try in order
        private static readonly string[] CandidateConnStrs = new[]
        {
            // Existing expected Express default instance
            "Server=.\\SQLEXPRESS;Database=BalanceDB;Trusted_Connection=True;TrustServerCertificate=True;",
            // LocalDB fallback (auto creates user instance)
            "Server=(localdb)\\MSSQLLocalDB;Integrated Security=true;Initial Catalog=BalanceDB;",
            // Generic localhost
            "Server=localhost;Database=BalanceDB;Trusted_Connection=True;TrustServerCertificate=True;",
        };

        public static string? LastErrorDetail { get; private set; }

        public static string? GetWorkingConnectionString()
        {
            foreach (var cs in CandidateConnStrs)
            {
                try
                {
                    using var conn = new SqlConnection(cs);
                    conn.Open();
                    // Ensure required tables exist
                    EnsureSchema(conn);
                    return cs; // success
                }
                catch (Exception ex)
                {
                    LastErrorDetail = ex.Message;
                }
            }
            return null; // none worked
        }

        private static void EnsureSchema(SqlConnection openConn)
        {
            // Create core tables if missing (minimal schema). Keep lightweight for demo.
            string sql = @"
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Registration')
BEGIN
    CREATE TABLE Registration(
        RegistrationID INT IDENTITY PRIMARY KEY,
        Username NVARCHAR(100) NOT NULL UNIQUE,
        PasswordHash NVARCHAR(200) NOT NULL,
        CreatedAt DATETIME NOT NULL DEFAULT GETDATE()
    );
    INSERT INTO Registration(Username,PasswordHash) VALUES('admin','admin');
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='DateOfTest')
BEGIN
    CREATE TABLE DateOfTest(
        TestDateID INT IDENTITY PRIMARY KEY,
        PatientID INT NOT NULL,
        TestDate DATETIME NOT NULL DEFAULT GETDATE()
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Test')
BEGIN
    CREATE TABLE Test(
        TestID INT IDENTITY PRIMARY KEY,
        TestDateID INT NOT NULL,
        ParameterID INT NOT NULL,
        Value FLOAT NULL
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Patients')
BEGIN
    CREATE TABLE Patients(
        PatientID INT IDENTITY PRIMARY KEY,
        FullName NVARCHAR(100),
        Gender NVARCHAR(10),
        DateOfBirth DATE,
        Address NVARCHAR(200),
        Phone NVARCHAR(20)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Doctors')
BEGIN
    CREATE TABLE Doctors (
        DoctorID INT IDENTITY PRIMARY KEY,
        FullName NVARCHAR(100),
        Department NVARCHAR(100),
        Phone NVARCHAR(20)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='NextOfKin')
BEGIN
    CREATE TABLE NextOfKin (
        KinID INT IDENTITY PRIMARY KEY,
        PatientID INT, 
        FullName NVARCHAR(100),
        Relation NVARCHAR(50),
        Phone NVARCHAR(20)
    );
END

IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name='Parameters')
BEGIN
    CREATE TABLE Parameters (
        ParameterID INT IDENTITY PRIMARY KEY,
        Name NVARCHAR(50),
        Unit NVARCHAR(20)
    );
END
";
            using (var cmd = new SqlCommand(sql, openConn))
            {
                cmd.ExecuteNonQuery();
            }
        }
    }
}

