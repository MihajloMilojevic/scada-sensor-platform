using System;

namespace AuthService.Models;

public class User
{
    public object Id { get; set; } = Guid.NewGuid();
    public string Username { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public string Role { get; set; } = "OPERATOR";
    public object CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}