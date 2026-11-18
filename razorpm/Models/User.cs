using System;
using System.Collections.Generic;

namespace razorpm.Models
{
    public class User
    {
        public int Id { get; set; }

        public string Login { get; set; } = "";
        public string Password { get; set; } = "";
        public string Name { get; set; } = "";
        public string Email { get; set; } = "";

        public string Role { get; set; } = "user";

        public string Theme { get; set; } = "light";
        public bool IsActive { get; set; } = true;

        public string? ResetPasswordToken { get; set; }
        public DateTime? ResetPasswordTokenExpiresAt { get; set; }

        public List<ProjectMember> ProjectMembers { get; set; } = new();
    }
}
