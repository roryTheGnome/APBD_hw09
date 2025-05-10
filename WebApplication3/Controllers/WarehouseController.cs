using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using WebApplication3.Models;

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

    [HttpPost]
    public async Task<IActionResult> RegisterProduct([FromBody] Product_Warehouse request, CancellationToken token)
    {
        await using var con = new SqlConnection(_configuration.GetConnectionString("DefaultConnection"));
        
        await con.OpenAsync(token);
        await using var transaction = await con.BeginTransactionAsync(token);


        try //according to the main script
        {
            //1
            var productChecker = new SqlCommand("SELECT 1 FROM Product WHERE IdProduct = @id", con,
                (SqlTransaction)transaction);
            productChecker.Parameters.AddWithValue("@id", request.IdProduct);
            if (await productChecker.ExecuteScalarAsync(token) is null)
                return NotFound("no such product exists.");


            //2
            var warhChecker = new SqlCommand("SELECT 1 FROM Warehouse WHERE IdWarehouse = @id", con,
                (SqlTransaction)transaction);
            warhChecker.Parameters.AddWithValue("@id", request.IdWarehouse);
            if (await warhChecker.ExecuteScalarAsync(token) is null)
                return NotFound("no such warehouse exists.");


            //3
            var cmd = new SqlCommand(@"
                SELECT TOP 1 IdOrder, Price
                FROM [Order]
                WHERE IdProduct = @idProduct AND Amount = @amount AND FulfilledAt IS NULL
                ORDER BY CreatedAt ASC", con, (SqlTransaction)transaction);
            cmd.Parameters.AddWithValue("@idProduct", request.IdProduct);
            cmd.Parameters.AddWithValue("@amount", request.Amount);

            int? idOrder = null;
            decimal price = 0;
            await using (var rdr = await cmd.ExecuteReaderAsync(token))
            {
                if (await rdr.ReadAsync(token))
                {
                    idOrder = rdr.GetInt32(0);
                    price = rdr.GetDecimal(1);
                }
                else
                {
                    return NotFound("no such order exists.");
                }
            }



            var duplicatedOrNot = new SqlCommand("SELECT 1 FROM Product_Warehouse WHERE IdOrder = @id", con,
                (SqlTransaction)transaction);
            duplicatedOrNot.Parameters.AddWithValue("@id", idOrder);
            if (await duplicatedOrNot.ExecuteScalarAsync(token) is not null)
                return Conflict("already exists.");



            //4
            var update = new SqlCommand("UPDATE [Order] SET FulfilledAt = GETDATE() WHERE IdOrder = @id", con,
                (SqlTransaction)transaction);
            update.Parameters.AddWithValue("@id", idOrder);
            await update.ExecuteNonQueryAsync(token);


            //5
            var insert = new SqlCommand(@"
                INSERT INTO Product_Warehouse (IdWarehouse, IdProduct, IdOrder, Amount, Price, CreatedAt)
                VALUES (@idWarehouse, @idProduct, @idOrder, @amount, @price, @createdAt);
                SELECT SCOPE_IDENTITY();
            ", con, (SqlTransaction)transaction);

            
            //6
            insert.Parameters.AddWithValue("@idWarehouse", request.IdWarehouse);
            insert.Parameters.AddWithValue("@idProduct", request.IdProduct);
            insert.Parameters.AddWithValue("@idOrder", idOrder);
            insert.Parameters.AddWithValue("@amount", request.Amount);
            insert.Parameters.AddWithValue("@price", price * request.Amount);
            insert.Parameters.AddWithValue("@createdAt", request.CreatedAt);

            var newId = await insert.ExecuteScalarAsync(token);

            await transaction.CommitAsync(token);
            return Ok($"Done");

        }
        catch
        {
            await transaction.RollbackAsync(token);
            return StatusCode(500);
        }
        
        return Ok();

    }

}