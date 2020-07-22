﻿using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace CName.PName.SName.EntityFrameworkCore
{
    public class SNameHttpApiHostMigrationsDbContextFactory : IDesignTimeDbContextFactory<SNameHttpApiHostMigrationsDbContext>
    {
        public SNameHttpApiHostMigrationsDbContext CreateDbContext(string[] args)
        {
            var configuration = BuildConfiguration();

            var builder = new DbContextOptionsBuilder<SNameHttpApiHostMigrationsDbContext>()
                .UseSqlServer(configuration.GetConnectionString("SName"));

            return new SNameHttpApiHostMigrationsDbContext(builder.Options);
        }

        private static IConfigurationRoot BuildConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false);

            return builder.Build();
        }
    }
}
