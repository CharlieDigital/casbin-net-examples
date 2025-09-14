---
applyTo: "**/*.cs"
---
# General Instructions
- This is a .NET project using C#.
- We are using .NET 9.0
- We are using Entity Framework Core for database access.
- We are using PostgreSQL as the database.
- We are using Casbin for authorization.

## Objective
- The objective of this set of tests is to explore how to use Casbin for authorization in a .NET application.
- To do so, we will build different policies and structures of objects and then test how they behave.
- Some tests we will want to test bulk actions in the database by "flattening" policies at runtime and passing them in

## How to Write and Run Tests
- We are writing TUnit Unit Tests
- NOT XUnit or NUnit
- Run the tests with `dotnet run` directly
