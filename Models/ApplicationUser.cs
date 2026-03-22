using Microsoft.AspNetCore.Identity;
using System;

namespace WardrobeApi.Models
{
    public class ApplicationUser : IdentityUser
    {
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}
