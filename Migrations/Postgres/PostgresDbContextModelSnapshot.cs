using System;
using Ae.Rail.Data;
using Ae.Rail.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

namespace Ae.Rail.Migrations.Postgres
{
	[DbContext(typeof(PostgresDbContext))]
	partial class PostgresDbContextModelSnapshot : ModelSnapshot
	{
		protected override void BuildModel(ModelBuilder modelBuilder)
		{
			modelBuilder
				.HasAnnotation("ProductVersion", "8.0.0")
				.HasAnnotation("Relational:MaxIdentifierLength", 63)
				.HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

			modelBuilder.Entity<MessageEnvelope>(b =>
			{
				b.ToTable("message_envelopes");
				b.HasKey("Id");

				b.Property<long>("Id")
					.HasColumnName("id")
					.HasColumnType("bigint")
					.ValueGeneratedOnAdd()
					.HasAnnotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn);

				b.Property<string>("Topic")
					.HasColumnName("topic")
					.HasColumnType("text");

				b.Property<int>("Partition")
					.HasColumnName("partition")
					.HasColumnType("integer");

				b.Property<long>("Offset")
					.HasColumnName("offset")
					.HasColumnType("bigint");

				b.Property<DateTime>("ReceivedAt")
					.HasColumnName("received_at")
					.HasColumnType("timestamp with time zone");

				b.Property<string>("MessageType")
					.HasColumnName("message_type")
					.HasColumnType("text");

				b.Property<System.Text.Json.JsonDocument>("Payload")
					.HasColumnName("payload")
					.HasColumnType("jsonb");

				b.HasIndex("Topic", "Partition", "Offset").IsUnique();
			});
		}
	}
}


