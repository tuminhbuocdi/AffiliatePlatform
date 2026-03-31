using System;
using System.Collections.Generic;

namespace Management.Domain.Entities
{
    public class Product
    {
        public long Id { get; set; }
        public string Platform { get; set; } = "Shopee";
        public string ExternalItemId { get; set; } = string.Empty;
        public string ShopeeItemId { get; set; } = string.Empty;

        // Import payload (top-level)
        public string? ProductLink { get; set; }
        public string? MaxCommissionRateRaw { get; set; }
        public string? SellerCommissionRateRaw { get; set; }
        public string? DefaultCommissionRateRaw { get; set; }

        // Import payload (batch_item_for_item_card_full)
        public string? ShopId { get; set; }

        // Raw JSON fields for arrays/objects
        public string? ImagesJson { get; set; }
        public string? VideoInfoListJson { get; set; }
        public string? TierVariationsJson { get; set; }
        public string? ItemRatingJson { get; set; }

        public int? OfferCardType { get; set; }

        public string? Currency { get; set; }
        public long? Stock { get; set; }
        public int? Status { get; set; }
        public long? Ctime { get; set; }
        public long? SoldRaw { get; set; }
        public string? SoldText { get; set; }
        public long? CatId { get; set; }
        public string? ItemStatus { get; set; }

        public long? PriceRaw { get; set; }
        public long? PriceMinRaw { get; set; }
        public long? PriceMaxRaw { get; set; }
        public long? PriceMinBeforeDiscountRaw { get; set; }
        public long? PriceMaxBeforeDiscountRaw { get; set; }
        public long? PriceBeforeDiscountRaw { get; set; }
        public string? Discount { get; set; }

        public int? ItemType { get; set; }
        public bool? ShopeeVerified { get; set; }
        public bool? IsOfficialShop { get; set; }
        public string? ShopLocation { get; set; }
        public bool? CanUseCod { get; set; }
        public bool? IsOnFlashSale { get; set; }
        public string? ShopName { get; set; }
        public float? ShopRating { get; set; }

        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Slug { get; set; }
        public string? ImageUrl { get; set; }
        public bool IsProcessed { get; set; }
        public DateTime? LastSynced { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        
        // Navigation properties
        public ICollection<ProductCategory> ProductCategories { get; set; } = new List<ProductCategory>();
        public ICollection<AffiliateClick> AffiliateClicks { get; set; } = new List<AffiliateClick>();
        public ICollection<AffiliateOrder> AffiliateOrders { get; set; } = new List<AffiliateOrder>();
    }
}
