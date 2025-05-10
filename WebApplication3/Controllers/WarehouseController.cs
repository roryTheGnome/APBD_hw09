using Microsoft.AspNetCore.Mvc;

namespace WebApplication3.Controllers;

[Route("api/[controller]")]
[ApiController]
public class WarehouseController:ControllerBase
{
    private readonly IConfiguration _configuration;

    public WarehouseController(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    
    
}