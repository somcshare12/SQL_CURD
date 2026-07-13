/* ===========================================================================
   SQL Data Manager — demonstration database
   ---------------------------------------------------------------------------
   Creates a safe demo database that exercises every metadata feature the
   module cares about:

     * identity primary keys        (Departments, Employees, Customers, Orders)
     * GUID primary key             (Products)
     * composite primary key        (OrderLines: OrderId + LineNumber)
     * rowversion concurrency        (every mutable table)
     * computed columns             (Employees.FullName, OrderLines.LineTotal)
     * database defaults            (CreatedAt, Status, IsActive, ...)
     * nullable columns             (Email, Salary, ...)
     * decimal precision            (Price, Salary, Total)
     * foreign keys                 (Employees→Departments, Orders→Customers, ...)
     * unique constraints           (Products.Sku)
     * a read-only reporting view   (reporting.MonthlySales)
     * a keyless table              (dbo.KeylessLog → update/delete disabled)
     * denied objects/schemas       (dbo.Users, dbo.AuditLog, internal.Secret)

   This script is idempotent: it drops and recreates the demo objects. It is
   NOT required in production.
   =========================================================================== */

-- Computed/persisted columns require these session settings to be ON.
SET QUOTED_IDENTIFIER ON;
SET ANSI_NULLS ON;
GO

IF DB_ID('SqlDataManagerDemo') IS NULL
BEGIN
    CREATE DATABASE SqlDataManagerDemo;
END;
GO

USE SqlDataManagerDemo;
GO

-- Schemas -------------------------------------------------------------------
IF SCHEMA_ID('sales') IS NULL EXEC('CREATE SCHEMA sales');
IF SCHEMA_ID('reporting') IS NULL EXEC('CREATE SCHEMA reporting');
IF SCHEMA_ID('internal') IS NULL EXEC('CREATE SCHEMA internal');
GO

-- Drop existing objects (child-first for FK safety) --------------------------
IF OBJECT_ID('reporting.MonthlySales', 'V') IS NOT NULL DROP VIEW reporting.MonthlySales;
IF OBJECT_ID('sales.OrderLines', 'U') IS NOT NULL DROP TABLE sales.OrderLines;
IF OBJECT_ID('sales.Orders', 'U') IS NOT NULL DROP TABLE sales.Orders;
IF OBJECT_ID('dbo.Employees', 'U') IS NOT NULL DROP TABLE dbo.Employees;
IF OBJECT_ID('dbo.Departments', 'U') IS NOT NULL DROP TABLE dbo.Departments;
IF OBJECT_ID('dbo.Products', 'U') IS NOT NULL DROP TABLE dbo.Products;
IF OBJECT_ID('dbo.Customers', 'U') IS NOT NULL DROP TABLE dbo.Customers;
IF OBJECT_ID('dbo.KeylessLog', 'U') IS NOT NULL DROP TABLE dbo.KeylessLog;
IF OBJECT_ID('dbo.Users', 'U') IS NOT NULL DROP TABLE dbo.Users;
IF OBJECT_ID('dbo.AuditLog', 'U') IS NOT NULL DROP TABLE dbo.AuditLog;
IF OBJECT_ID('internal.Secret', 'U') IS NOT NULL DROP TABLE internal.Secret;
GO

-- dbo.Departments (identity PK, rowversion) ---------------------------------
CREATE TABLE dbo.Departments
(
    DepartmentId INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Departments PRIMARY KEY,
    Name         NVARCHAR(100)     NOT NULL,
    CostCentre   NVARCHAR(20)      NULL,
    CreatedAt    DATETIME2         NOT NULL CONSTRAINT DF_Departments_CreatedAt DEFAULT SYSUTCDATETIME(),
    RowVersion   ROWVERSION        NOT NULL
);
GO

-- dbo.Customers (identity PK, sensitive column, default status, rowversion) -
CREATE TABLE dbo.Customers
(
    CustomerId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Customers PRIMARY KEY,
    CustomerName NVARCHAR(200)     NOT NULL,
    Email        NVARCHAR(256)     NULL,
    NationalId   NVARCHAR(50)      NULL,
    Status       NVARCHAR(20)      NOT NULL CONSTRAINT DF_Customers_Status DEFAULT N'Pending',
    CreatedAt    DATETIME2         NOT NULL CONSTRAINT DF_Customers_CreatedAt DEFAULT SYSUTCDATETIME(),
    RowVersion   ROWVERSION        NOT NULL
);
GO

-- dbo.Products (GUID PK, unique SKU, decimal price, rowversion) -------------
CREATE TABLE dbo.Products
(
    ProductId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT PK_Products PRIMARY KEY
                                CONSTRAINT DF_Products_Id DEFAULT NEWID(),
    Sku        NVARCHAR(50)     NOT NULL CONSTRAINT UQ_Products_Sku UNIQUE,
    Name       NVARCHAR(200)    NOT NULL,
    Price      DECIMAL(18,2)    NOT NULL CONSTRAINT DF_Products_Price DEFAULT 0,
    InStock    BIT              NOT NULL CONSTRAINT DF_Products_InStock DEFAULT 1,
    RowVersion ROWVERSION       NOT NULL
);
GO

-- dbo.Employees (identity PK, FK, computed column, defaults, rowversion) ----
CREATE TABLE dbo.Employees
(
    EmployeeId   INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Employees PRIMARY KEY,
    DepartmentId INT               NOT NULL CONSTRAINT FK_Employees_Departments
                                   REFERENCES dbo.Departments(DepartmentId),
    FirstName    NVARCHAR(100)     NOT NULL,
    LastName     NVARCHAR(100)     NOT NULL,
    FullName     AS (FirstName + N' ' + LastName) PERSISTED,
    Email        NVARCHAR(256)     NULL,
    Salary       DECIMAL(18,2)     NULL,
    HireDate     DATE              NOT NULL CONSTRAINT DF_Employees_HireDate DEFAULT (CONVERT(date, SYSUTCDATETIME())),
    IsActive     BIT               NOT NULL CONSTRAINT DF_Employees_IsActive DEFAULT 1,
    RowVersion   ROWVERSION        NOT NULL
);
GO

-- sales.Orders (identity PK, FK to Customers, rowversion) -------------------
CREATE TABLE sales.Orders
(
    OrderId    INT IDENTITY(1,1) NOT NULL CONSTRAINT PK_Orders PRIMARY KEY,
    CustomerId INT               NOT NULL CONSTRAINT FK_Orders_Customers
                                 REFERENCES dbo.Customers(CustomerId),
    OrderDate  DATETIME2         NOT NULL CONSTRAINT DF_Orders_OrderDate DEFAULT SYSUTCDATETIME(),
    Total      DECIMAL(18,2)     NOT NULL CONSTRAINT DF_Orders_Total DEFAULT 0,
    Notes      NVARCHAR(1000)    NULL,
    RowVersion ROWVERSION        NOT NULL
);
GO

-- sales.OrderLines (composite PK, FKs, computed line total) -----------------
CREATE TABLE sales.OrderLines
(
    OrderId    INT              NOT NULL CONSTRAINT FK_OrderLines_Orders
                                REFERENCES sales.Orders(OrderId),
    LineNumber INT              NOT NULL,
    ProductId  UNIQUEIDENTIFIER NOT NULL CONSTRAINT FK_OrderLines_Products
                                REFERENCES dbo.Products(ProductId),
    Quantity   INT              NOT NULL CONSTRAINT DF_OrderLines_Quantity DEFAULT 1,
    UnitPrice  DECIMAL(18,2)    NOT NULL,
    LineTotal  AS (Quantity * UnitPrice) PERSISTED,
    CONSTRAINT PK_OrderLines PRIMARY KEY (OrderId, LineNumber)
);
GO

-- reporting.MonthlySales (read-only view) -----------------------------------
CREATE VIEW reporting.MonthlySales
AS
    SELECT
        YEAR(o.OrderDate)  AS SalesYear,
        MONTH(o.OrderDate) AS SalesMonth,
        COUNT_BIG(*)       AS OrderCount,
        SUM(o.Total)       AS TotalSales
    FROM sales.Orders o
    GROUP BY YEAR(o.OrderDate), MONTH(o.OrderDate);
GO

-- dbo.KeylessLog (no primary key → update/delete must be disabled) ----------
CREATE TABLE dbo.KeylessLog
(
    Message  NVARCHAR(200) NOT NULL,
    LoggedAt DATETIME2     NOT NULL CONSTRAINT DF_KeylessLog_LoggedAt DEFAULT SYSUTCDATETIME()
);
GO

-- Objects that configuration DENIES (must never appear / be reachable) ------
CREATE TABLE dbo.Users     (UserId INT IDENTITY PRIMARY KEY, UserName NVARCHAR(100) NOT NULL);
CREATE TABLE dbo.AuditLog  (AuditId INT IDENTITY PRIMARY KEY, Detail NVARCHAR(400) NOT NULL);
CREATE TABLE internal.Secret (SecretId INT IDENTITY PRIMARY KEY, SecretValue NVARCHAR(400) NOT NULL);
GO

/* ---- Seed data ---------------------------------------------------------- */

INSERT INTO dbo.Departments (Name, CostCentre) VALUES
    (N'Engineering', N'ENG'),
    (N'Sales',       N'SAL'),
    (N'Support',     N'SUP');

INSERT INTO dbo.Customers (CustomerName, Email, NationalId, Status) VALUES
    (N'Ali Khan',       N'ali.khan@example.com',   N'35201-1234567-1', N'Active'),
    (N'Sara Ahmed',     N'sara.ahmed@example.com', N'35202-7654321-2', N'Pending'),
    (N'Contoso Ltd',    N'accounts@contoso.com',   NULL,               N'Active'),
    (N'Northwind Trade',N'hello@northwind.com',    NULL,               N'Suspended');

INSERT INTO dbo.Products (Sku, Name, Price, InStock) VALUES
    (N'SKU-001', N'Wireless Mouse',    19.99, 1),
    (N'SKU-002', N'Mechanical Keyboard', 79.50, 1),
    (N'SKU-003', N'27\" Monitor',      219.00, 1),
    (N'SKU-004', N'USB-C Dock',        149.00, 0);

INSERT INTO dbo.Employees (DepartmentId, FirstName, LastName, Email, Salary) VALUES
    (1, N'Grace', N'Hopper',   N'grace@example.com',  120000.00),
    (1, N'Alan',  N'Turing',   N'alan@example.com',   118000.00),
    (2, N'Ada',   N'Lovelace', N'ada@example.com',     95000.00),
    (3, N'Linus', N'Torvalds', NULL,                    88000.00);

INSERT INTO sales.Orders (CustomerId, Total, Notes) VALUES
    (1, 99.49, N'First order'),
    (1, 219.00, NULL),
    (3, 298.00, N'Bulk purchase');

DECLARE @p1 UNIQUEIDENTIFIER = (SELECT ProductId FROM dbo.Products WHERE Sku = N'SKU-001');
DECLARE @p2 UNIQUEIDENTIFIER = (SELECT ProductId FROM dbo.Products WHERE Sku = N'SKU-002');
DECLARE @p3 UNIQUEIDENTIFIER = (SELECT ProductId FROM dbo.Products WHERE Sku = N'SKU-003');

INSERT INTO sales.OrderLines (OrderId, LineNumber, ProductId, Quantity, UnitPrice) VALUES
    (1, 1, @p1, 1, 19.99),
    (1, 2, @p2, 1, 79.50),
    (2, 1, @p3, 1, 219.00),
    (3, 1, @p3, 1, 219.00),
    (3, 2, @p2, 1, 79.00);

INSERT INTO dbo.KeylessLog (Message) VALUES (N'System started'), (N'Nightly job ran');
GO

PRINT 'SqlDataManagerDemo seeded successfully.';
GO
