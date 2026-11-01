var builder = DistributedApplication.CreateBuilder(args);

var sqlite = builder.AddSqlite("sqlite", databaseFileName: "grades.db")
    .WithSqliteWeb();

builder.AddProject<Projects.NexGrades_App>("wpf-app")
    .WithReference(sqlite);

builder.Build().Run();
