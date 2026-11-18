using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using razorpm.Data;
using razorpm.Models;
using razorpm.Services;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorPages();

builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddDistributedMemoryCache();

builder.Services.Configure<EmailSettings>(
    builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddScoped<EmailService>();
builder.Services.AddScoped<IPasswordHasher<User>, PasswordHasher<User>>();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromHours(2);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();

    db.Database.Migrate();

    SeedDemoData(db, hasher);
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseSession();

app.MapRazorPages();

app.Run();

static void SeedDemoData(AppDbContext db, IPasswordHasher<User> hasher)
{
    var adminUser = db.Users.FirstOrDefault(u => u.Login == "admin");
    if (adminUser == null)
    {
        adminUser = new User
        {
            Login = "admin",
            Name = "Администратор",
            Role = "admin",
            Theme = "light",
            IsActive = true
        };
        adminUser.Password = hasher.HashPassword(adminUser, "Admin#4826Work");
        db.Users.Add(adminUser);
        db.SaveChanges();
    }

    var demoUser = db.Users.FirstOrDefault(u => u.Login == "demo");
    if (demoUser == null)
    {
        demoUser = new User
        {
            Login = "demo",
            Name = "Пользователь Demo",
            Role = "user",
            Theme = "light",
            IsActive = true
        };
        demoUser.Password = hasher.HashPassword(demoUser, "Demo#7319Task");
        db.Users.Add(demoUser);
        db.SaveChanges();
    }

    if (!db.Projects.Any())
    {
        var p1 = new Project
        {
            Name = "Проект 1",
            Status = "active",
            IsArchived = false,
            OwnerId = adminUser.Id,
            OwnerName = adminUser.Name
        };

        var p2 = new Project
        {
            Name = "Проект 2",
            Status = "active",
            IsArchived = false,
            OwnerId = demoUser.Id,
            OwnerName = demoUser.Name
        };

        var p3 = new Project
        {
            Name = "Проект 3",
            Status = "done",
            IsArchived = false,
            OwnerId = adminUser.Id,
            OwnerName = adminUser.Name
        };

        db.Projects.AddRange(p1, p2, p3);
        db.SaveChanges();

        db.ProjectMembers.Add(new ProjectMember
        {
            ProjectId = p1.Id,
            UserId = demoUser.Id
        });
        db.SaveChanges();

        db.Tasks.AddRange(
            new TaskItem
            {
                Title = "Задача 1",
                Description = "Описание задачи 1",
                Priority = "high",
                Status = "active",
                AssigneeUserId = adminUser.Id,
                StartDate = new DateTime(2026, 3, 10),
                DueDate = new DateTime(2026, 3, 20),
                SpentHours = 6,
                ProjectId = p1.Id
            },
            new TaskItem
            {
                Title = "Задача 2",
                Description = "Описание задачи 2",
                Priority = "medium",
                Status = "done",
                AssigneeUserId = demoUser.Id,
                StartDate = new DateTime(2026, 3, 5),
                DueDate = new DateTime(2026, 3, 12),
                SpentHours = 4,
                ProjectId = p2.Id
            },
            new TaskItem
            {
                Title = "Задача 3",
                Description = "Описание задачи 3",
                Priority = "low",
                Status = "active",
                AssigneeUserId = adminUser.Id,
                StartDate = new DateTime(2026, 3, 18),
                DueDate = new DateTime(2026, 3, 25),
                SpentHours = 2,
                ProjectId = p3.Id
            }
        );
        db.SaveChanges();
    }
}
