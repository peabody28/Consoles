using Microsoft.AspNetCore.Mvc;


namespace NodesWeb.Controllers
{
    public class HomeController : Controller
    {
        public HomeController()
        {

        }

        [HttpGet]
        public ActionResult Index()
        {
            return View();
        }
    }
}
