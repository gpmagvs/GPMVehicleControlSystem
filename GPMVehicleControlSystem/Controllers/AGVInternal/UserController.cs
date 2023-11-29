using AGVSystemCommonNet6.User;
using AGVSystemCommonNet6.Vehicle_Control.VCSDatabase;
using GPMVehicleControlSystem.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace GPMVehicleControlSystem.Controllers.AGVInternal
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        [HttpPost("Login")]
        public async Task<IActionResult> Login(UserEntity user)
        {
            var user_query_out = DBhelper.QueryUserByName(user.UserName);
            if (user_query_out == null)
            {

                return Ok(new LoginResult { Success = false, Message = $"UserName({user.UserName}) Not Exist" });
            }
            else
            {
                if (user_query_out.Password == user.Password)
                {
                    return Ok(new LoginResult()
                    {
                        Success = true,
                        UserName = user_query_out.UserName,
                        Role = user_query_out.Role
                    });
                }
                else
                {
                    return Ok(new LoginResult { Success = false, Message = $"Incorrect password" });
                }
            }
        }
    }
}
