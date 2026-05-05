using MdNoteToGithub.Models;
using Microsoft.EntityFrameworkCore;

namespace MdNoteToGithub.DataBase;

public class BotDbContext : DbContext
{
    public DbSet<UserSettings> Users { get; set; }

    protected override void OnConfiguring(
        DbContextOptionsBuilder optionsBuilder
    )
    {
        optionsBuilder.UseSqlite("Data Source=bot_database.db");
    }

    protected override void OnModelCreating(
        ModelBuilder modelBuilder
    )
    {
        modelBuilder.Entity<UserSettings>()
                    .HasKey(u => u.TelegramId);
    }
}