using Ae.Rail.Models;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace Ae.Rail.Data
{
	public sealed class PostgresDbContext : DbContext
	{
		public DbSet<MessageEnvelope> MessageEnvelopes { get; set; }
		public DbSet<TrainService> TrainServices { get; set; }
		public DbSet<Vehicle> Vehicles { get; set; }
		public DbSet<ServiceVehicle> ServiceVehicles { get; set; }

		// Views (legacy, for backward compatibility during migration)
		public DbSet<TrainServiceView> TrainServiceViews { get; set; }
		public DbSet<VehicleView> VehicleViews { get; set; }
		public DbSet<ServiceVehicleView> ServiceVehicleViews { get; set; }

		public PostgresDbContext(DbContextOptions<PostgresDbContext> options) : base(options)
		{
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			// Message envelopes (raw audit trail)
			var env = modelBuilder.Entity<MessageEnvelope>();
			env.ToTable("message_envelopes");
			env.HasKey(x => x.Id);
			env.Property(x => x.Id).HasColumnName("id");
			env.Property(x => x.ReceivedAt).HasColumnName("received_at").HasColumnType("timestamp with time zone");
			env.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");

			// Train services table
			var ts = modelBuilder.Entity<TrainService>();
			ts.ToTable("train_services");
			ts.HasKey(x => x.Id);

			// Vehicles table
			var v = modelBuilder.Entity<Vehicle>();
			v.ToTable("vehicles");
			v.HasKey(x => x.Id);

			// Service vehicles table
			var sv = modelBuilder.Entity<ServiceVehicle>();
			sv.ToTable("service_vehicles");
			sv.HasKey(x => x.Id);

			// Legacy view mappings (kept for backward compatibility during migration)
			var tsv = modelBuilder.Entity<TrainServiceView>();
			tsv.HasNoKey();
			tsv.ToView("trainservice_current");
			tsv.Property(x => x.OperationalTrainNumber).HasColumnName("OperationalTrainNumber");
			tsv.Property(x => x.ServiceDate).HasColumnName("ServiceDate");
			tsv.Property(x => x.OriginStd).HasColumnName("OriginStd");
			tsv.Property(x => x.Sta).HasColumnName("Sta");
			tsv.Property(x => x.OriginLocationPrimaryCode).HasColumnName("OriginLocationPrimaryCode");
			tsv.Property(x => x.DestLocationPrimaryCode).HasColumnName("DestLocationPrimaryCode");
			tsv.Property(x => x.OriginLocationName).HasColumnName("OriginLocationName");
			tsv.Property(x => x.DestLocationName).HasColumnName("DestLocationName");
			tsv.Property(x => x.TrainOriginDateTime).HasColumnName("TrainOriginDateTime");
			tsv.Property(x => x.TrainDestDateTime).HasColumnName("TrainDestDateTime");
			tsv.Property(x => x.ToiCore).HasColumnName("ToiCore");
			tsv.Property(x => x.ToiVariant).HasColumnName("ToiVariant");
			tsv.Property(x => x.ToiTimetableYear).HasColumnName("ToiTimetableYear");
			tsv.Property(x => x.ToiStartDate).HasColumnName("ToiStartDate");
			tsv.Property(x => x.RailClasses).HasColumnName("RailClasses");
			tsv.Property(x => x.PowerType).HasColumnName("PowerType");
			tsv.Property(x => x.LastUpdatedAt).HasColumnName("LastUpdatedAt");

			var vv = modelBuilder.Entity<VehicleView>();
			vv.HasNoKey();
			vv.ToView("vehicle_current");
			vv.Property(x => x.VehicleId).HasColumnName("VehicleId");
			vv.Property(x => x.SpecificType).HasColumnName("SpecificType");
			vv.Property(x => x.TypeOfVehicle).HasColumnName("TypeOfVehicle");
			vv.Property(x => x.NumberOfCabs).HasColumnName("NumberOfCabs");
			vv.Property(x => x.NumberOfSeats).HasColumnName("NumberOfSeats");
			vv.Property(x => x.LengthUnit).HasColumnName("LengthUnit");
			vv.Property(x => x.LengthMm).HasColumnName("LengthMm");
			vv.Property(x => x.Weight).HasColumnName("Weight");
			vv.Property(x => x.MaximumSpeed).HasColumnName("MaximumSpeed");
			vv.Property(x => x.TrainBrakeType).HasColumnName("TrainBrakeType");
			vv.Property(x => x.Livery).HasColumnName("Livery");
			vv.Property(x => x.Decor).HasColumnName("Decor");
			vv.Property(x => x.VehicleStatus).HasColumnName("VehicleStatus");
			vv.Property(x => x.RegisteredStatus).HasColumnName("RegisteredStatus");
			vv.Property(x => x.RegisteredCategory).HasColumnName("RegisteredCategory");
			vv.Property(x => x.DateRegistered).HasColumnName("DateRegistered");
			vv.Property(x => x.DateEnteredService).HasColumnName("DateEnteredService");
			vv.Property(x => x.ResourcePosition).HasColumnName("ResourcePosition");
			vv.Property(x => x.PlannedResourceGroup).HasColumnName("PlannedResourceGroup");
			vv.Property(x => x.ResourceGroupId).HasColumnName("ResourceGroupId");
			vv.Property(x => x.FleetId).HasColumnName("FleetId");
			vv.Property(x => x.TypeOfResource).HasColumnName("TypeOfResource");
			vv.Property(x => x.IsLocomotive).HasColumnName("IsLocomotive");
			vv.Property(x => x.ClassCode).HasColumnName("ClassCode");
			vv.Property(x => x.PowerType).HasColumnName("PowerType");
			vv.Property(x => x.IsDrivingVehicle).HasColumnName("IsDrivingVehicle");
			vv.Property(x => x.LastUpdatedAt).HasColumnName("LastUpdatedAt");

			var svv = modelBuilder.Entity<ServiceVehicleView>();
			svv.HasNoKey();
			svv.ToView("service_vehicle_current");
			svv.Property(x => x.OperationalTrainNumber).HasColumnName("OperationalTrainNumber");
			svv.Property(x => x.ServiceDate).HasColumnName("ServiceDate");
			svv.Property(x => x.OriginStd).HasColumnName("OriginStd");
			svv.Property(x => x.VehicleId).HasColumnName("VehicleId");
			svv.Property(x => x.SpecificType).HasColumnName("SpecificType");
			svv.Property(x => x.TypeOfVehicle).HasColumnName("TypeOfVehicle");
			svv.Property(x => x.NumberOfCabs).HasColumnName("NumberOfCabs");
			svv.Property(x => x.NumberOfSeats).HasColumnName("NumberOfSeats");
			svv.Property(x => x.LengthUnit).HasColumnName("LengthUnit");
			svv.Property(x => x.LengthMm).HasColumnName("LengthMm");
			svv.Property(x => x.Weight).HasColumnName("Weight");
			svv.Property(x => x.MaximumSpeed).HasColumnName("MaximumSpeed");
			svv.Property(x => x.TrainBrakeType).HasColumnName("TrainBrakeType");
			svv.Property(x => x.Livery).HasColumnName("Livery");
			svv.Property(x => x.Decor).HasColumnName("Decor");
			svv.Property(x => x.VehicleStatus).HasColumnName("VehicleStatus");
			svv.Property(x => x.RegisteredStatus).HasColumnName("RegisteredStatus");
			svv.Property(x => x.RegisteredCategory).HasColumnName("RegisteredCategory");
			svv.Property(x => x.DateRegistered).HasColumnName("DateRegistered");
			svv.Property(x => x.DateEnteredService).HasColumnName("DateEnteredService");
			svv.Property(x => x.ResourcePosition).HasColumnName("ResourcePosition");
			svv.Property(x => x.PlannedResourceGroup).HasColumnName("PlannedResourceGroup");
			svv.Property(x => x.ResourceGroupId).HasColumnName("ResourceGroupId");
			svv.Property(x => x.FleetId).HasColumnName("FleetId");
			svv.Property(x => x.TypeOfResource).HasColumnName("TypeOfResource");
			svv.Property(x => x.IsLocomotive).HasColumnName("IsLocomotive");
			svv.Property(x => x.ClassCode).HasColumnName("ClassCode");
			svv.Property(x => x.LastUpdatedAt).HasColumnName("LastUpdatedAt");
		}
	}
}


