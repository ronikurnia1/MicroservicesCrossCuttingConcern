﻿using GloboTicket.Web.Extensions;
using GloboTicket.Web.Models;
using GloboTicket.Web.Models.Api;
using GloboTicket.Web.Models.View;
using GloboTicket.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace GloboTicket.Web.Controllers
{
    public class ShoppingBasketController : Controller
    {
        private readonly IShoppingBasketService basketService;
        private readonly IDiscountService discountService;
        private readonly ILogger<ShoppingBasketController> logger;
        private readonly Settings settings;

        public ShoppingBasketController(IShoppingBasketService basketService, Settings settings, IDiscountService discountService, ILogger<ShoppingBasketController> logger)
        {
            this.basketService = basketService;
            this.settings = settings;
            this.discountService = discountService;
            this.logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var basketViewModel = await CreateBasketViewModel();

            return View(basketViewModel);
        }

        private async Task<BasketViewModel> CreateBasketViewModel()
        {
            var basketId = Request.Cookies.GetCurrentBasketId(settings);
            Basket basket = await basketService.GetBasket(basketId);

            var basketLines = await basketService.GetLinesForBasket(basketId);

            var lineViewModels = basketLines.Select(bl => new BasketLineViewModel
            {
                LineId = bl.BasketLineId,
                EventId = bl.EventId,
                EventName = bl.Event.Name,
                Date = bl.Event.Date,
                Price = bl.Price,
                Quantity = bl.TicketAmount
            });


            var basketViewModel = new BasketViewModel
            {
                BasketLines = lineViewModels.ToList()
            };

            Coupon coupon = null;

            if (basket.CouponId.HasValue)
                coupon = await discountService.GetCouponById(basket.CouponId.Value);

            if (coupon != null)
            {
                basketViewModel.Code = coupon.Code;
                basketViewModel.Discount = coupon.Amount;

                
            }

            basketViewModel.ShoppingCartTotal = basketViewModel.BasketLines.Sum(bl => bl.Price * bl.Quantity);

            return basketViewModel;
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddLine(BasketLineForCreation basketLine)
        {
            var basketId = Request.Cookies.GetCurrentBasketId(settings);
            var newLine = await basketService.AddToBasket(basketId, basketLine);
            Response.Cookies.Append(settings.BasketIdCookieName, newLine.BasketId.ToString());

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateLine(BasketLineForUpdate basketLineUpdate)
        {
            var basketId = Request.Cookies.GetCurrentBasketId(settings);
            await basketService.UpdateLine(basketId, basketLineUpdate);
            return RedirectToAction("Index");
        }

        public async Task<IActionResult> RemoveLine(Guid lineId)
        {
            var basketId = Request.Cookies.GetCurrentBasketId(settings);
            await basketService.RemoveLine(basketId, lineId);
            return RedirectToAction("Index");
        }

        public IActionResult Checkout()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(BasketCheckoutViewModel basketCheckoutViewModel)
        {
            using var scope = logger.BeginScope("Checking out basket {BasketId}", basketCheckoutViewModel.BasketId);
            
            try
            {
                var basketId = Request.Cookies.GetCurrentBasketId(settings);
                if (ModelState.IsValid)
                {
                    var basketForCheckout = new BasketForCheckout
                    {
                        FirstName = basketCheckoutViewModel.FirstName,
                        LastName = basketCheckoutViewModel.LastName,
                        Email = basketCheckoutViewModel.Email,
                        Address = basketCheckoutViewModel.Address,
                        ZipCode = basketCheckoutViewModel.ZipCode,
                        City = basketCheckoutViewModel.City,
                        Country = basketCheckoutViewModel.Country,
                        CardNumber = basketCheckoutViewModel.CardNumber,
                        CardName = basketCheckoutViewModel.CardName,
                        CardExpiration = basketCheckoutViewModel.CardExpiration,
                        CvvCode = basketCheckoutViewModel.CvvCode,
                        BasketId = basketId,
                        UserId = settings.UserId
                    };

                    await basketService.Checkout(basketId, basketForCheckout);

                    return RedirectToAction("CheckoutComplete");
                }

                return View(basketCheckoutViewModel);
            }
            catch (Exception e)
            {
                ViewBag.ErrorMessage = e.Message;
                return View(basketCheckoutViewModel);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ApplyDiscountCode(string code)
        {
            var basketId = Request.Cookies.GetCurrentBasketId(settings);

            using var scope = logger.BeginScope("Applying {CouponCode} to basket {BasketId}", code, basketId);

            var coupon = await discountService.GetCouponByCode(code);

            if (coupon == null || coupon.AlreadyUsed)
            {
                if (coupon == null)
                {
                    logger.LogInformation("Coupon code does not exist");
                }
                else
                {                  
                    logger.LogWarning("User attempted to reuse a previous coupon code");
                }

                return RedirectToAction("Index");
            }

            //coupon will be applied to the basket
            await basketService.ApplyCouponToBasket(basketId, new CouponForUpdate() { CouponId = coupon.CouponId });
            await discountService.UseCoupon(coupon.CouponId);

            logger.LogDebug("Applied discount coupon to basket", coupon.CouponId, basketId);

            return RedirectToAction("Index");
        }

        public IActionResult CheckoutComplete()
        {
            return View();
        }
    }
}