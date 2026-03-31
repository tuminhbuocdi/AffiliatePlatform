namespace Management.Domain.Entities
{
    public class ProductCategory
    {
        public long ProductId { get; set; }
        public int CategoryId { get; set; }
        
        // Navigation properties
        public Product Product { get; set; } = null!;
        public Category Category { get; set; } = null!;
    }
}
