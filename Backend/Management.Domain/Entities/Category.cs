using System;

namespace Management.Domain.Entities
{
    public class Category
    {
        public int Id { get; set; }
        public string Platform { get; set; } = "Shopee";
        public string ExternalCategoryId { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? Slug { get; set; }
        public int? ParentId { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Navigation properties
        public Category? ParentCategory { get; set; }
        public ICollection<Category> ChildCategories { get; set; } = new List<Category>();
        public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
    }
}
