# Student API System

A full .NET 8 student management system built with **ASP.NET Core Web API**, **JWT authentication**, **refresh tokens**, **role-based authorization**, and a small **console client** for testing the API.

## Features

- JWT login with short-lived access tokens
- Refresh token flow with token rotation
- Logout endpoint that revokes refresh tokens
- Role-based authorization (`Admin`)
- Custom policy to allow a student to access only their own record or allow admin access
- Rate limiting on authentication endpoints
- Swagger/OpenAPI documentation with JWT Bearer support
- CRUD operations for students
- Upload and retrieve student images
- SQL Server integration through stored procedures

## Solution Structure

```text
Full System/
├── StudentApi-ServerSide/
│   ├── ApiRestStudent/         # ASP.NET Core Web API
│   ├── DTOS/                   # Data transfer objects
│   ├── StudentApiBusinessLayer/# Business logic
│   └── StudentDataAcessLayer/  # SQL Server data access
└── StudentApi-ClientSide/      # Console client for testing the API
```

## Technologies Used

- .NET 8
- ASP.NET Core Web API
- JWT Bearer Authentication
- BCrypt.Net-Next
- Microsoft.Data.SqlClient
- Swagger / Swashbuckle

## Requirements

- .NET 8 SDK
- SQL Server
- Visual Studio 2022 or VS Code
- Git

## Database Setup

The server connects to a SQL Server database named `StudentsDB`.

Update the connection string in:

```text
StudentApi-ServerSide/StudentDataAcessLayer/ClsDataStudent.cs
```

The project expects stored procedures such as:

- `SP_GetAllStudents`
- `SP_GetPassedStudents`
- other student CRUD procedures used by the data layer

## Configuration

### JWT Secret

Set `JWT_SECRET_KEY` before running the API.

You can add it through user secrets, environment variables, or local app settings.

### Image Upload Path

The API stores uploaded images in:

```text
H:\UploadImages
```

If needed, change this path inside `StudentController.cs`.

## How to Run

### 1) Run the API

Open the solution in the server folder and start the web project:

```bash
cd StudentApi-ServerSide/ApiRestStudent
dotnet run
```

### 2) Run the console client

```bash
cd StudentApi-ClientSide
dotnet run
```

The client is configured to call:

```text
https://localhost:7088/
```

## API Endpoints

### Auth

- `POST /api/Auth/login`
- `POST /api/Auth/Refresh`
- `POST /api/Auth/logout`

### Students

- `GET /api/Student/All` — Admin only
- `GET /api/Student/Passed` — Anonymous
- `GET /api/Student/AverageGrade` — Anonymous
- `GET /api/Student/{ID}` — Student owner or Admin
- `POST /api/Student` — Admin only
- `PUT /api/Student/{ID}` — Admin only
- `DELETE /api/Student/{ID}` — Admin only
- `POST /api/Student/UbloadImage` — Admin only
- `GET /api/Student/GetImage/{ImageName}` — Admin only

## Notes

- The console client contains demo credentials in `Program.cs`; replace them with your own test user.
- The API uses short-lived access tokens, so the refresh token flow is important for continued access.
- Some files in the repository are build artifacts (`bin`, `obj`, `.vs`). They are usually excluded from GitHub using `.gitignore`.

## License

No license has been specified yet.
