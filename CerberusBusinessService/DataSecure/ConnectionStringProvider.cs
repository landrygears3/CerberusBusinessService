namespace CerberusBusinessService.DataSecure
{
    public class ConnectionStringProvider
    {
        public string ConnectionString { get; }

        public ConnectionStringProvider(string connectionString)
        {
            ConnectionString = connectionString;
        }
    }
}
