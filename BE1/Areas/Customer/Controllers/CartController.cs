using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using BE1.Models;
using BE1.Models.viewmodel; // Khớp với namespace ViewModel của dự án bạn

namespace BE1.Areas.Customer.Controllers
{
    public class CartController : Controller
    {
        // Sử dụng chung DbContext của dự án
        private MyStoreEntities1 db = new MyStoreEntities1();

        // Lấy giỏ hàng từ Session
        private List<CartItemViewModel> GetSessionCart()
        {
            var cart = Session["Cart"] as List<CartItemViewModel>;
            if (cart == null)
            {
                cart = new List<CartItemViewModel>();
                Session["Cart"] = cart;
            }
            return cart;
        }

        // GET: Customer/Cart/Index
        public ActionResult Index()
        {
            var cart = GetSessionCart();
            return View(cart);
        }

        // POST: Customer/Cart/AddToCart
        [HttpPost]
        public ActionResult AddToCart(int productId, int quantity = 1)
        {
            var product = db.Products.Find(productId);
            if (product == null)
            {
                return HttpNotFound();
            }

            var cart = GetSessionCart();
            var item = cart.FirstOrDefault(p => p.ProductID == productId);

            // Dự án của bạn dùng ProductPrice làm giá bán chính thức
            decimal finalPrice = product.ProductPrice;

            if (item != null)
            {
                item.Quantity += quantity;
                item.Price = finalPrice;
            }
            else
            {
                cart.Add(new CartItemViewModel
                {
                    ProductID = product.ProductID,
                    Title = product.ProductName, // Khớp với trường ProductName trong model của bạn
                    Price = finalPrice,
                    Image = product.ProductImage,
                    Quantity = quantity
                });
            }

            Session["Cart"] = cart;
            return RedirectToAction("Index");
        }

        // POST: Customer/Cart/UpdateQuantity
        [HttpPost]
        public ActionResult UpdateQuantity(int productId, int quantity)
        {
            var cart = GetSessionCart();
            var item = cart.FirstOrDefault(p => p.ProductID == productId);
            if (item != null)
            {
                if (quantity > 0)
                {
                    item.Quantity = quantity;
                }
                else
                {
                    cart.Remove(item);
                }
            }
            Session["Cart"] = cart;
            return RedirectToAction("Index");
        }

   
        public ActionResult RemoveItem(int productId)
        {
            var cart = GetSessionCart();
            var item = cart.FirstOrDefault(p => p.ProductID == productId);
            if (item != null)
            {
                cart.Remove(item);
            }
            Session["Cart"] = cart;
            return RedirectToAction("Index");
        }
        // GET: Customer/Cart/Reorder/5
        // GET: Customer/Cart/Reorder/5
        public ActionResult Reorder(int orderId)
        {
            var order = db.Orders
                          .FirstOrDefault(o => o.OrderID == orderId);

            if (order == null)
            {
                return RedirectToAction("MyOrder", "Order", new { area = "Customer" });
            }

            // Lấy danh sách chi tiết đơn hàng tương ứng
            var details = db.OrderDetails.Where(d => d.OrderID == orderId).ToList();
            if (details == null || !details.Any())
            {
                return RedirectToAction("MyOrder", "Order", new { area = "Customer" });
            }

            var cart = GetSessionCart();

            foreach (var detail in details)
            {
                var product = db.Products.Find(detail.ProductID);
                if (product == null) continue;

                var existingItem = cart.FirstOrDefault(p => p.ProductID == detail.ProductID);
                decimal priceToUse = detail.UnitPrice > 0 ? detail.UnitPrice : product.ProductPrice;

                if (existingItem != null)
                {
                    existingItem.Quantity += detail.Quantity;
                    existingItem.Price = priceToUse;
                }
                else
                {
                    cart.Add(new CartItemViewModel
                    {
                        ProductID = detail.ProductID,
                        Title = product.ProductName,
                        Price = priceToUse,
                        Image = product.ProductImage,
                        Quantity = 1
                    });
                }
            }

            Session["Cart"] = cart;
            return RedirectToAction("Index", "Cart", new { area = "Customer" });
        }
        public ActionResult CartBadge()
        {
            var cart = Session["Cart"] as List<CartItemViewModel>;
            int totalQty = (cart != null && cart.Any()) ? cart.Sum(x => x.Quantity) : 0;
            return Content($"<span class=\"cart-badge\">{totalQty}</span>");
        }


        public ActionResult CartSubmenu()
        {
            var cart = Session["Cart"] as List<CartItemViewModel>;
            return PartialView("_CartSubmenu", cart);
        }
    }
}