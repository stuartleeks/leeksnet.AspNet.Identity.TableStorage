using System.Web;
using Microsoft.AspNet.Identity;
using Microsoft.WindowsAzure.Storage.Table;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.WindowsAzure.Storage;

namespace leeksnet.AspNet.Identity.TableStorage
{
    public class IdentityUser : TableEntity, IUser
    {
        public string Id { get { return UserName; } } // Keeping it simple and mapping IUser.Id to IUser.UserName. This allows easy, efficient lookup by setting this to be the rowkey in table storage
        public string UserName
        {
            get { return RowKey; }
            set { RowKey = value; }
        }

        public string PasswordHash { get; set; }

        public string SerializedLogins // Simple way to keep logins in the same table :-)
        {
            get
            {
                if (_logins == null || _logins.Count == 0)
                    return null;
                return JsonConvert.SerializeObject(Logins);
            }
            set
            {
                _logins = JsonConvert.DeserializeObject<IList<UserLoginInfo>>(value) ?? new List<UserLoginInfo>();
            }
        }

        private IList<UserLoginInfo> _logins;
        [IgnoreProperty]
        public IList<UserLoginInfo> Logins
        {
            get
            {
                _logins = _logins ?? new List<UserLoginInfo>();
                return _logins;
            }
            set { _logins = value; }
        }
    }

}
