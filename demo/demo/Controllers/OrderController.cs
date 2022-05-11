using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using demo.Models;
using PayPal.Api;
using Order = demo.Models.Order;

namespace demo.Controllers
{
    public class OrderController : Controller
    {
        LapTrinhWebEntities db = new LapTrinhWebEntities();
        // GET: Order
        public ActionResult Index()
        {
            int userID = int.Parse(Session["idUser"].ToString());
            List<Order> orders = db.Orders.Where(x => x.UserID == userID).ToList();
            return View(orders);
        }

        // GET: Order/Details/5
        public ActionResult Details(int id)
        {
            //Order order = db.Orders.Include("OrderItems").Where(x => x.OrderID == id).First();

            Order order1 = db.Orders.Find(id);
            List<OrderItem> orderItems = db.OrderItems.Where(x => x.OrderID == id).ToList();
            order1.OrderItems = orderItems;

            return View(order1);
        }

        //// GET: Order/Create
        //public ActionResult Create()
        //{
        //    return View();
        //}

        // POST: Order/Create
        
    
        private Payment payment;
        private Payment CreatePayment(APIContext aPIContext, string redirectUrl)
        {
            var listItem = new ItemList()
            {
                items = new List<Item>()
            };
            List<ClotheItem> cart = Session["cart"] as List<ClotheItem>;
            foreach (var Cart in cart)
            {
                listItem.items.Add(new Item()
                {
                    name = Cart.Name,
                    currency = "USD",
                    price = Cart.Price.ToString(),
                    quantity = Cart.Quantity.ToString(),
                    sku = "sku"
                });
            }
            var payer = new Payer()
            {
                payment_method = "paypal"
            };
            var redirUrls = new RedirectUrls()
            {
                cancel_url = redirectUrl + "&Cancel=true",
                return_url = redirectUrl
            };
            var details = new Details()
            {
                tax = "1",
                shipping = "1",
                subtotal = cart.Sum(m => m.Total).ToString("#,##0").Replace(',', '.')
            };
            var amount = new Amount()
            {
                currency = "USD",
                total = (Convert.ToDouble(details.tax) + Convert.ToDouble(details.shipping) + Convert.ToDouble(details.subtotal)).ToString(), // Total must be equal to sum of tax, shipping and subtotal.  
                details = details
            };
            var transactionList = new List<Transaction>();
            // Adding description about the transaction  
            transactionList.Add(new Transaction()
            {
                description = "Transaction description",
                invoice_number = Convert.ToString((new Random()).Next(100000)),
                amount = amount,
                item_list = listItem
            });
            this.payment = new Payment()
            {
                intent = "sale",
                payer = payer,
                transactions = transactionList,
                redirect_urls = redirUrls
            };
            // Create a payment using a APIContext  
            return this.payment.Create(aPIContext);
        }
        private Payment ExecutePayment(APIContext apiContext, string payerId, string paymentId)
        {
            var paymentExecution = new PaymentExecution()
            {
                payer_id = payerId,
            };
            payment = new Payment() { id = paymentId };
            return payment.Execute(apiContext, paymentExecution);
        }
        [HttpPost]
        public ActionResult PaymentWithPaypal()
        {
            List<ClotheItem> cart = Session["cart"] as List<ClotheItem>;
            Order order = new Order();
            order.UserID = int.Parse(Session["idUser"].ToString());
            order.Total = (int)cart.Sum(x => x.Total);
            order.OrderItems = new List<OrderItem>();



            if (Session["idUser"] == null)
            {
                ViewBag.error = "do login";
                return RedirectToAction("Login", "Account");
            }
            else
            {
                APIContext apiContext = PayPalConfiguration.GetAPIContext();
                try
                {
                    string payerId = Request.Params["PayerID"];
                    if (string.IsNullOrEmpty(payerId))
                    {

                        string baseURI = Request.Url.Scheme + "://" + Request.Url.Authority + "/Order/Index?";

                        var guid = Convert.ToString((new Random()).Next(100000));

                        var createdPayment = this.CreatePayment(apiContext, baseURI + "guid=" + guid);

                        var links = createdPayment.links.GetEnumerator();
                        string paypalRedirectUrl = null;
                        while (links.MoveNext())
                        {
                            Links lnk = links.Current;
                            if (lnk.rel.ToLower().Trim().Equals("approval_url"))
                            {
                                //saving the payapalredirect URL to which user will be redirected for payment  
                                paypalRedirectUrl = lnk.href;
                            }
                        }
                        // saving the paymentID in the key guid  
                        Session.Add(guid, createdPayment.id);
                        foreach (ClotheItem item in cart)
                        {
                            order.OrderItems.Add(new OrderItem
                            {
                                ClotheID = item.ClotheId,
                                Quantity = item.Quantity
                            });
                        }
                        db.Orders.Add(order);
                        db.SaveChanges();
                        return Redirect(paypalRedirectUrl);
                    }
                    else
                    {
                        // This function exectues after receving all parameters for the payment  
                        var guid = Request.Params["guid"];
                        var executedPayment = ExecutePayment(apiContext, payerId, Session[guid] as string);
                        //If executed payment failed then we will show payment failure message to user  
                        if (executedPayment.state.ToLower() != "approved")
                        {
                            return View("Failure");
                        }
                    }
                }
       
                catch (Exception ex)
                {
                    PaypalLogger.Log("Erro" + ex.Message);
                    return View("Failure");
                }
                
                return View("Index");

            }

        }
    }
}
