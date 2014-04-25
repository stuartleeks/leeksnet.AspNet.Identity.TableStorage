using Microsoft.AspNet.Identity;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace leeksnet.AspNet.Identity.TableStorage
{
    public class UserStore<TUser> :
        IUserStore<TUser>,
        IUserPasswordStore<TUser>,
        IUserLoginStore<TUser>,
        IUserRoleStore<TUser>,
        IUserEmailStore<TUser>
        where TUser : IdentityUser
    {
        private readonly Func<string, string> _partitionKeyFromId;
        private readonly CloudTable _userTableReference;
        private readonly CloudTable _loginTableReference;
        private readonly CloudTable _emailTableReference;

        /// <summary>
        /// 
        /// </summary>
        /// <param name="connectionString">Storage connection string</param>
        public UserStore(string connectionString, Func<string, string> partitionKeyFromId)
        {
            _partitionKeyFromId = partitionKeyFromId;
            CloudStorageAccount storageAccount = CloudStorageAccount.Parse(connectionString);
            var tableClient = storageAccount.CreateCloudTableClient();
            _userTableReference = tableClient.GetTableReference("Users");
            _userTableReference.CreateIfNotExists();

            _loginTableReference = tableClient.GetTableReference("Logins");
            _loginTableReference.CreateIfNotExists();

            _emailTableReference = tableClient.GetTableReference("Email");
            _emailTableReference.CreateIfNotExists();
        }

        private CloudTable GetUserTable()
        {
            return _userTableReference;
        }

        private CloudTable GetLoginTable()
        {
            return _loginTableReference;
        }
        private CloudTable GetEmailTable()
        {
            return _emailTableReference;
        }

        public void Dispose()
        {
        }

        public async Task CreateAsync(TUser user)
        {
            var partitionKey = _partitionKeyFromId(user.Id);
            user.PartitionKey = partitionKey;
            var operation = TableOperation.Insert(user);
            await GetUserTable().ExecuteAsync(operation);
            if (!string.IsNullOrEmpty(user.Email))
            {
                await SaveUserEmailMappingAsync(user.Id, user.Email);
            }
            foreach (var userLoginInfo in user.Logins)
            {
                await AddLoginAsync(user, userLoginInfo);
            }
        }

        public async Task UpdateAsync(TUser user)
        {
            var partitionKey = _partitionKeyFromId(user.Id);
            user.PartitionKey = partitionKey;
            await UpdateUser(user);
        }

        public async Task DeleteAsync(TUser user)
        {
            if (!string.IsNullOrEmpty(user.Email))
            {
                await DeleteUserEmailMappingAsync(user.Id, user.Email);
            }
            foreach (var userLoginInfo in user.Logins)
            {
                await RemoveLoginAsync(user, userLoginInfo);
            }
            user.ETag = "*";
            var operation = TableOperation.Delete(user);
            await GetUserTable().ExecuteAsync(operation);
        }

        public async Task<TUser> FindByIdAsync(string userId)
        {
            var partitionKey = _partitionKeyFromId(userId);
            var operation = TableOperation.Retrieve<TUser>(partitionKey, userId);
            TableResult result = await GetUserTable().ExecuteAsync(operation);
            return (TUser)result.Result;
        }

        public Task<TUser> FindByNameAsync(string userName)
        {
            return FindByIdAsync(userName);
        }

        public Task SetPasswordHashAsync(TUser user, string passwordHash)
        {
            user.PasswordHash = passwordHash;
            return Task.FromResult(0);
        }

        public Task<string> GetPasswordHashAsync(TUser user)
        {
            return Task.FromResult(user.PasswordHash);
        }

        public Task<bool> HasPasswordAsync(TUser user)
        {
            return Task.FromResult(string.IsNullOrEmpty(user.PasswordHash));
        }

        public async Task AddLoginAsync(TUser user, UserLoginInfo login)
        {
            user.Logins.Add(login);
            await UpdateUser(user);

            var operation = TableOperation.Insert(new LoginInfoEntity(login, user.Id));
            await GetLoginTable().ExecuteAsync(operation);
        }

        public async Task RemoveLoginAsync(TUser user, UserLoginInfo login)
        {
            var userLogin = user.Logins.FirstOrDefault(l => l.ProviderKey == login.ProviderKey && l.LoginProvider == login.LoginProvider);
            if (userLogin != null)
            {
                user.Logins.Remove(userLogin);
                await UpdateUser(user);
            }

            var operation = TableOperation.Delete(new LoginInfoEntity(login, user.Id) { ETag = "*" });
            await GetLoginTable().ExecuteAsync(operation);
        }

        public Task<IList<UserLoginInfo>> GetLoginsAsync(TUser user)
        {
            return Task.FromResult(user.Logins);
        }

        public async Task<TUser> FindAsync(UserLoginInfo login)
        {
            var lie = new LoginInfoEntity(login, "");
            var operation = TableOperation.Retrieve<LoginInfoEntity>(lie.PartitionKey, lie.RowKey);
            var result = await GetLoginTable().ExecuteAsync(operation);
            var loginInfoEntity = (LoginInfoEntity)result.Result;
            if (loginInfoEntity == null)
                return null;
            return await FindByIdAsync(loginInfoEntity.UserId);
        }

        public async Task AddToRoleAsync(TUser user, string role)
        {
            if (!user.Roles.Contains(role))
            {
                user.Roles.Add(role);
                await UpdateUser(user);
            }
        }

        public async Task RemoveFromRoleAsync(TUser user, string role)
        {
            if (user.Roles.Remove(role))
            {
                await UpdateUser(user);
            }
        }

        public Task<IList<string>> GetRolesAsync(TUser user)
        {
            return Task.FromResult(user.Roles);
        }

        public Task<bool> IsInRoleAsync(TUser user, string role)
        {
            return Task.FromResult(user.Roles.Contains(role));
        }


        private async Task UpdateUser(TUser user)
        {
            var operation = TableOperation.Replace(user);
            await GetUserTable().ExecuteAsync(operation);
            if (!string.IsNullOrEmpty(user.Email))
            {
                await SaveUserEmailMappingAsync(user.Id, user.Email);
            }
        }

        public async Task SetEmailAsync(TUser user, string email)
        {
            var userOriginal = await FindByIdAsync(user.Id);
            if (!string.IsNullOrEmpty(userOriginal.Email))
            {
                // clear old mapping
                await DeleteUserEmailMappingAsync(user.Id, userOriginal.Email);
            }
            await UpdateUser(user); // saves email mapping :-)

        }

        private async Task DeleteUserEmailMappingAsync(string userId, string email)
        {
            var partitionKey = _partitionKeyFromId(email);
            var operationDeleteOldEmail =
                TableOperation.Delete(new EmailToUserEntity(partitionKey, userId, email) { ETag = "*" });
            await GetLoginTable().ExecuteAsync(operationDeleteOldEmail);
        }

        private async Task SaveUserEmailMappingAsync(string userId, string email)
        {
            var partitionKeyNewEmail = _partitionKeyFromId(email);
            var operationAddNewEmail = TableOperation.Insert(new EmailToUserEntity(partitionKeyNewEmail, userId, email));
            await GetEmailTable().ExecuteAsync(operationAddNewEmail);
        }

        public Task<string> GetEmailAsync(TUser user)
        {
            return Task.FromResult(user.Email);
        }

        public Task<bool> GetEmailConfirmedAsync(TUser user)
        {
            return Task.FromResult(user.EmailConfirmed);
        }

        public async Task SetEmailConfirmedAsync(TUser user, bool confirmed)
        {
            user.EmailConfirmed = confirmed;
            await UpdateAsync(user);
        }

        public async Task<TUser> FindByEmailAsync(string email)
        {
            var partitionKey = _partitionKeyFromId(email);
            var operation = TableOperation.Retrieve<EmailToUserEntity>(partitionKey, email);
            TableResult result = await GetEmailTable().ExecuteAsync(operation);
            var emailMapping = (EmailToUserEntity)result.Result;
            if (emailMapping == null)
            {
                return null;
            }
            var user = await FindByIdAsync(emailMapping.UserId);
            return user;
        }
    }
}
