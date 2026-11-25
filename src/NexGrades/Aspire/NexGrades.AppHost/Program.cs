var builder = DistributedApplication.CreateBuilder(args);

var sqlite = builder.AddSqlite("sqlite", databaseFileName: "grades.db")
    .WithSqliteWeb();

builder.AddDotnetProject("wpf-app", "../../NexGrades.App/NexGrades.App.csproj")
    .WithReference(sqlite);

builder.Build().Run();
