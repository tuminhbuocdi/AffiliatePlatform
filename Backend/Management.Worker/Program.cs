namespace Management.Worker
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = Host.CreateApplicationBuilder(args);

            builder.Services.AddSingleton<Management.Infrastructure.Db.DbConnectionFactory>();

            var host = builder.Build();
            host.Run();
        }
    }
}