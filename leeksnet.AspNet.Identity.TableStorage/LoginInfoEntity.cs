using System.Web;
using Microsoft.AspNet.Identity;
using Microsoft.WindowsAzure.Storage.Table;

namespace leeksnet.AspNet.Identity.TableStorage
{
    public class LoginInfoEntity : TableEntity
    {
        public string UserId { get; set; }

        public LoginInfoEntity()
        {

        }
        public LoginInfoEntity(UserLoginInfo loginInfo, string userId)
        {
            UserId = userId;
            this.PartitionKey = EncodeKey(loginInfo.LoginProvider);
            this.RowKey = EncodeKey(loginInfo.ProviderKey);
        }

        private static string EncodeKey(string key)
        {
            return HttpUtility.UrlEncode(key);
        }
    }
}
