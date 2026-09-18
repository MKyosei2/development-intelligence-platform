// Purpose: Defines the DIP responsibility represented by P ol ic ie s.
namespace Dip.Api.Authorization;

public static class Policies
{
    public const string ProjectRead = "project-read";
    public const string ProjectWrite = "project-write";
    public const string Admin = "admin";
}

public static class Roles
{
    public const string Member = "Member";
    public const string Admin = "Admin";
}
