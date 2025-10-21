var builder = DistributedApplication.CreateBuilder(args);

var sqlite = builder.AddSqlite("grades-db", databaseFileName: "app.db")
    .WithSqliteWeb();

builder.AddProject<Projects.NexGrades_App>("wpf-app")
    .WithReference(sqlite);

builder.Build().Run();
