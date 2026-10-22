using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace FarmhouseBakery.Models
{
    public class MenuItem
    {
        public int Id { get; set; }

        [Required]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Range(0.01, 9999.99)]
        [Precision(10, 2)] // SQL Server: decimal(10,2), e.g. 12.50
        public decimal Price { get; set; }

        [Required]
        public string Ingredients { get; set; } = string.Empty;

        [Required]
        public string Category { get; set; } = string.Empty;

        public string? ImageUrl { get; set; }

        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    }
}