using Domio.Application.Users;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Domio.Web.Controllers;

[Authorize(Roles = "Administrator")]
public sealed class UsersController(
    IUserDirectoryService userDirectoryService) : Controller
{
    [HttpGet]
    public async Task<IActionResult> Index(
        CancellationToken cancellationToken = default)
    {
        var overview =
            await userDirectoryService.GetOverviewAsync(
                cancellationToken);

        return View(overview);
    }
}
