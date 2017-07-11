using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace LeanCode.Example.Migrations
{
    [DbContext(typeof(ExampleDbContext))]
    partial class ExampleDbContextModelSnapshot : ModelSnapshot
    {
        protected override void BuildModel(ModelBuilder modelBuilder)
        {
            modelBuilder
                .HasAnnotation("ProductVersion", "1.1.1");

            modelBuilder.Entity("LeanCode.PushNotifications.EF.PushNotificationTokenEntity", b =>
                {
                    b.Property<Guid>("Id")
                        .ValueGeneratedOnAdd();

                    b.Property<DateTime>("DateCreated");

                    b.Property<int>("DeviceType");

                    b.Property<string>("Token")
                        .IsRequired()
                        .HasMaxLength(1024);

                    b.Property<Guid>("UserId");

                    b.HasKey("Id");

                    b.HasIndex("Token");

                    b.HasIndex("UserId", "DeviceType");

                    b.ToTable("Tokens");
                });
        }
    }
}
