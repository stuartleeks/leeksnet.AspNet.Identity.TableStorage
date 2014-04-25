using Microsoft.WindowsAzure.Storage.Table;

namespace leeksnet.AspNet.Identity.TableStorage
{
    public class EmailToUserEntity : TableEntity
    {
        public EmailToUserEntity()
        {

        }
        public EmailToUserEntity(string partitionKey, string userId, string email)
        {
            this.PartitionKey = partitionKey;
            this.RowKey = email;
            this.UserId = userId;
        }

        public string Email { get { return RowKey; } }
        public string UserId { get; set; }
    }
}