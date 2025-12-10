using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using FastFoodOrderingSystem.Helpers;

namespace FastFoodOrderingSystem.Models
{
    public class Product
    {
        [Key]
        public int Id { get; set; }

        [Required(ErrorMessage = "Product name is required")]
        [StringLength(100, ErrorMessage = "Name cannot exceed 100 characters")]
        public string Name
        {
            get => _name;
            set => _name = SecurityHelper.SanitizeInput(value);
        }
        private string _name;

        [StringLength(500, ErrorMessage = "Description cannot exceed 500 characters")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Price is required")]
        [Range(0.01, 1000, ErrorMessage = "Price must be between $0.01 and $1000")]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Price { get; set; }

        [Required(ErrorMessage = "Category is required")]
        [StringLength(50, ErrorMessage = "Category cannot exceed 50 characters")]
        public string Category { get; set; }

        [Url(ErrorMessage = "Please enter a valid URL")]
        [StringLength(255, ErrorMessage = "Image URL cannot exceed 255 characters")]
        public string ImageUrl { get; set; }

        public bool IsAvailable { get; set; } = true;
    }
}