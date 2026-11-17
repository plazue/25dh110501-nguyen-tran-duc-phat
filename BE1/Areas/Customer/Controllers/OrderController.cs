using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.Entity.Validation;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using BE1.Models;
using BE1.Models.viewmodel;

namespace BE1.Areas.Customer.Controllers
{
    public class OrderController : Controller
    {
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

        // GET: Customer/Order/Checkout
        public ActionResult Checkout()
        {
            var cart = GetSessionCart();
            if (cart == null || !cart.Any())
            {
                return RedirectToAction("Index", "Cart", new { area = "Customer" });
            }

            int? customerId = Session["UserID"] as int?;
            if (customerId != null)
            {
                var customer = db.Customers.Find(customerId.Value);
                ViewBag.Customer = customer;
            }

            return View(cart);
        }

        // POST: Customer/Order/PlaceOrder
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult PlaceOrder(string fullName, string phone, string address, string note, string paymentMethod, string deliveryMethod)
        {
            var cart = GetSessionCart();
            if (cart == null || !cart.Any())
            {
                return RedirectToAction("Index", "Cart", new { area = "Customer" });
            }

            try
            {
                foreach (var item in cart)
                {
                    var productInDb = db.Products.AsNoTracking().FirstOrDefault(p => p.ProductID == item.ProductID);
                    if (productInDb == null || productInDb.Quantity < item.Quantity)
                    {
                        ModelState.AddModelError("", $"Sản phẩm '{item.Title}' không đủ số lượng trong kho.");
                        int? custId = Session["UserID"] as int?;
                        if (custId != null) { ViewBag.Customer = db.Customers.Find(custId.Value); }
                        return View("Checkout", cart);
                    }
                }

                int? customerId = Session["UserID"] as int?;
                if (customerId != null)
                {
                    var customerInDb = db.Customers.Find(customerId.Value);
                    if (customerInDb != null)
                    {
                        customerInDb.CustomerName = fullName;
                        customerInDb.CustomerPhone = phone;
                        customerInDb.CustomerAddress = address;
                    }
                }

                decimal totalAmount = cart.Sum(x => x.Total);

                // KIỂM TRA PHƯƠNG THỨC THANH TOÁN ĐỂ BỎ QUA BƯỚC CHỜ THANH TOÁN NẾU LÀ COD
                string initialStatus = "Chờ thanh toán";
                if (!string.IsNullOrEmpty(paymentMethod) && paymentMethod.Equals("COD", StringComparison.OrdinalIgnoreCase))
                {
                    initialStatus = "Chờ xử lý"; 
                }

                var order = new Order
                {
                    CustomerID = customerId ?? 0,
                    OrderDate = DateTime.Now,
                    TotalAmount = totalAmount,
                    PaymentStatus = initialStatus,
                    ShippingAddress = address,
                    DeliveryMethod = string.IsNullOrEmpty(deliveryMethod) ? "Giao hàng tiêu chuẩn" : deliveryMethod,
                    PaymentMethod = string.IsNullOrEmpty(paymentMethod) ? "COD" : paymentMethod
                };

                db.Orders.Add(order);
                db.SaveChanges();

                foreach (var item in cart)
                {
                    var orderDetail = new OrderDetail
                    {
                        OrderID = order.OrderID,
                        ProductID = item.ProductID,
                        Quantity = item.Quantity,
                        UnitPrice = item.Price,
                        TotalPrice = item.Total
                    };
                    db.OrderDetails.Add(orderDetail);

                    db.Database.ExecuteSqlCommand(
                        "UPDATE Product SET Quantity = CASE WHEN Quantity - {0} < 0 THEN 0 ELSE Quantity - {0} END WHERE ProductID = {1}",
                        item.Quantity, item.ProductID
                    );
                }

                db.SaveChanges();
                Session["Cart"] = null;

                return RedirectToAction("OrdersSuccess", new { area = "Customer", id = order.OrderID });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Lỗi khi xử lý đơn hàng: " + ex.Message);
                int? custId = Session["UserID"] as int?;
                if (custId != null) { ViewBag.Customer = db.Customers.Find(custId.Value); }
                return View("Checkout", cart);
            }
        }
        // POST: Customer/Order/CancelOrder/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CancelOrder(int id)
        {
            int? customerId = Session["UserID"] as int?;
            if (customerId == null) return RedirectToAction("Login", "Account", new { area = "Customer" });

            var order = db.Orders.FirstOrDefault(o => o.OrderID == id && o.CustomerID == customerId.Value);
            if (order == null) return HttpNotFound();

            if (order.PaymentStatus == "Chờ thanh toán" || order.PaymentStatus == "Chờ xử lý" || order.PaymentStatus == "Đã xử lý " || order.PaymentStatus == "Đang giao hàng")
            {
                order.PaymentStatus = "Đã hủy";


                foreach (var detail in order.OrderDetails)
                {
                    var product = db.Products.Find(detail.ProductID);
                    if (product != null)
                    {
                        product.Quantity += detail.Quantity;
                    }
                }

                db.SaveChanges();
            }

            return RedirectToAction("MyOrder", new { area = "Customer" });
        }

        // GET: Customer/Order/OrdersSuccess/5
        public ActionResult OrdersSuccess(int? id)
        {
            if (id == null) return RedirectToAction("ProductList", "Home1", new { area = "Customer" });

            var order = db.Orders.Include(o => o.OrderDetails.Select(d => d.Product)).FirstOrDefault(o => o.OrderID == id);
            if (order == null) return HttpNotFound();

            return View(order);
        }

    
        public ActionResult MyOrder(string searchTerm, string status)
        {
            int? customerId = Session["UserID"] as int?;
            if (customerId == null) return RedirectToAction("Login", "Account", new { area = "Customer" });

            var query = db.Orders
                .Include(o => o.OrderDetails.Select(d => d.Product))
                .Where(o => o.CustomerID == customerId.Value)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                searchTerm = searchTerm.Trim();
                int orderIdSearch;
                bool isNumber = int.TryParse(searchTerm, out orderIdSearch);

                query = query.Where(o => (isNumber && o.OrderID == orderIdSearch) ||
                                         o.OrderDetails.Any(d => d.Product.ProductName.Contains(searchTerm)));
                ViewBag.SearchTerm = searchTerm;
            }

            if (!string.IsNullOrEmpty(status) && status != "Tất cả")
            {
                query = query.Where(o => o.PaymentStatus == status);
                ViewBag.SelectedStatus = status;
            }
            else
            {
                ViewBag.SelectedStatus = "Tất cả";
            }

            var orders = query.OrderByDescending(o => o.OrderDate).ToList();
            return View(orders);
        }

        public ActionResult OrderHistory()
        {
            int? customerId = Session["UserID"] as int?;
            if (customerId == null) return RedirectToAction("Login", "Account", new { area = "Customer" });

            var orders = db.Orders
                .Include(o => o.OrderDetails.Select(d => d.Product))
                .Where(o => o.CustomerID == customerId.Value)
                .OrderByDescending(o => o.OrderDate)
                .ToList();

            return View(orders);
        }

        // GET: Customer/Order/Details/5
        public ActionResult Details(int? id)
        {
            if (id == null) return RedirectToAction("MyOrder", "Order", new { area = "Customer" });

            int? customerId = Session["UserID"] as int?;
            if (customerId == null) return RedirectToAction("Login", "Account", new { area = "Customer" });
        
            var order = db.Orders
                          .Include(o => o.OrderDetails.Select(d => d.Product))
                          .FirstOrDefault(o => o.OrderID == id && o.CustomerID == customerId.Value);
         
            if (order == null) return HttpNotFound();

            return View(order);
         
           
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing) db.Dispose();
            base.Dispose(disposing);
        }
    }
}