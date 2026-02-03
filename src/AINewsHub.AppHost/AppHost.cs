var builder = DistributedApplication.CreateBuilder(args);

// Add SQLite database resource (shared by all services)
var sqliteConnection = builder.AddParameter("sqlite-connection",
    builder.Configuration["ConnectionStrings:DefaultConnection"] ?? "Data Source=../../../data/ainewshub.db");

// Add Web project
var web = builder.AddProject<Projects.AINewsHub_Web>("web")
    .WithEnvironment("ConnectionStrings__DefaultConnection", sqliteConnection);

// Add Crawler Service
var crawler = builder.AddProject<Projects.AINewsHub_CrawlerService>("crawler")
    .WithEnvironment("ConnectionStrings__DefaultConnection", sqliteConnection);

// Add Newsletter Service
var newsletter = builder.AddProject<Projects.AINewsHub_NewsletterService>("newsletter")
    .WithEnvironment("ConnectionStrings__DefaultConnection", sqliteConnection);

builder.Build().Run();
