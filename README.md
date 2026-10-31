# POS & E-Commerce Management System API

A RESTful backend API for a Point of Sale (POS) and E-Commerce Management System, developed using **ASP.NET Core 9 Web API** and **Microsoft SQL Server**.

The project provides backend functionality for product management, inventory tracking, order processing, payment handling, user authentication, and return management.

## 🚀 Technologies Used

| Technology | Purpose |
|---|---|
| C# | Backend Programming |
| ASP.NET Core 9 Web API | REST API Development |
| Entity Framework Core 9 | ORM |
| Microsoft SQL Server | Database |
| ASP.NET Core Identity | User Management |
| JWT Bearer Authentication | API Authentication |
| Swagger / OpenAPI | API Documentation |
| SQL Server Stored Procedures | Database Operations |
| SSLCommerz | Online Payment Integration |

## ✨ Features

### 1. Authentication & Authorization
- User registration and login
- JWT-based authentication
- Role-based authorization
- ASP.NET Core Identity integration
- Protected API endpoints

### 2. Product Management
- Create and manage products
- Product categories
- Product barcode management
- Product image upload
- Product details and pricing
- Search products by barcode

### 3. Inventory Management
- Inventory records
- Product stock management

### 4. Order Management
- Order creation and processing
- Order items management
- Order status management
- Delivery address management
- Multiple order types

### 5. Payment Management
- Payment method management
- Payment records
- SSLCommerz payment gateway integration
- Payment validation and callback handling

### 6. Return Management
- Product return processing
- Return items management
- Order return tracking

### 7. Database Operations
- Code First approach
- Entity Framework Core migrations
- SQL Server integration
- Stored procedure-based product queries

## 📁 Project Structure

```text
POS-ECommerce-Management-System/
│
├── POSWebApi/
│   ├── Controllers/
│   │   ├── AccountController.cs
│   │   ├── CategoryController.cs
│   │   ├── InventoryController.cs
│   │   ├── OrderController.cs
│   │   ├── PaymentController.cs
│   │   ├── PaymentMethodController.cs
│   │   ├── ProductsController.cs
│   │   ├── ReturnController.cs
│   │   └── RoleController.cs
│   │
│   ├── DTOs/
│   ├── Migrations/
│   ├── Models/
│   ├── Services/
│   │   └── SslCommerzService.cs
│   │
│   ├── wwwroot/
│   ├── appsettings.json
│   ├── Program.cs
│   └── POSWebApi.csproj
│
├── POSWebApi.sln
└── README.md
```

## ⚙️ Getting Started

### Prerequisites

Make sure you have installed:

- .NET 9 SDK
- Visual Studio 2022 or later
- Microsoft SQL Server
- SQL Server Management Studio (SSMS)
- Git

### 1. Clone the Repository

```bash
git clone https://github.com/YOUR_USERNAME/POS-ECommerce-Management-System.git
```

### 2. Navigate to the Project

```bash
cd POS-ECommerce-Management-System
```

### 3. Configure Database Connection

Update the connection string in your local configuration.

```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=YOUR_SERVER;Database=POSDB;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```

### 4. Configure JWT Authentication

Set your own JWT configuration using User Secrets or environment variables. Never commit production JWT keys.

### 5. Configure SSLCommerz

Add your SSLCommerz sandbox credentials through secure configuration.

For testing, use sandbox credentials. Switch to production credentials only when your merchant account is ready.

### 6. Apply Database Migrations

Install Entity Framework Core tools if necessary:

```bash
dotnet tool install --global dotnet-ef
```

Apply migrations:

```bash
dotnet ef database update --project POSWebApi/POSWebApi.csproj
```

### 7. Run the Application

```bash
dotnet run --project POSWebApi/POSWebApi.csproj
```

The API will run using the configured ASP.NET Core launch profile.

Swagger documentation is enabled in the Development environment.

## 🔐 Security Considerations

- Use strong JWT signing keys.
- Keep database credentials outside source control.
- Store payment gateway credentials securely.
- Use HTTPS in production.
- Apply proper role-based authorization.
- Validate incoming API requests.
- Restrict CORS to trusted frontend origins in production.

## 🛠️ Development Approach

This project uses a **Code First** database development approach with Entity Framework Core.

Database schema changes are managed through EF Core migrations. Selected product queries use SQL Server stored procedures.

## 📌 Future Improvements

- Sales reporting and analytics
- Dashboard APIs
- Advanced inventory tracking
- Multi-branch POS support
- Automated testing
- Docker deployment
- API versioning
- Advanced logging and monitoring

## 👨‍💻 Author

**MD. Kayum Hossain**

GitHub: https://github.com/kayumsntx

---

⭐ If you find this project useful, feel free to explore the repository and share your feedback.

**License:** Add an appropriate open-source license before allowing others to reuse or distribute this project.
