using Microsoft.AspNet.Identity;
using Microsoft.WindowsAzure.Storage;
using Microsoft.WindowsAzure.Storage.Table;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace leeksnet.AspNet.Identity.TableStorage
{
    public class UserStore<TUser> :
        IUserPasswordStore<TUser>,
        IUserLoginStore<TUser>,
        IUserRoleStore<TUser> 
        where TUser : IdentityUser
    {
        private readonly Func<string, string> _partitionKeyFromId;
        private readonly CloudTable _userTableReference;
        private readonly CloudTable _loginTableReference;

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
        }

        private CloudTable GetUserTable()
        {
            return _userTableReference;
        }

        private CloudTable GetLoginTable()
        {
            return _loginTableReference;
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
        }

        public async Task UpdateAsync(TUser user)
        {
            var partitionKey = _partitionKeyFromId(user.Id);
            user.PartitionKey = partitionKey;
            await UpdateUser(user);
        }

        public async Task DeleteAsync(TUser user)
        {
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
            user.Logins.Remove(login);
            await UpdateUser(user);
            
            var operation = TableOperation.Delete(new LoginInfoEntity(login, user.Id) {ETag = "*"});
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
        }
    }
}
