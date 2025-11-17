using Ae.Rail.Models;
using Microsoft.EntityFrameworkCore;
using NpgsqlTypes;

namespace Ae.Rail.Data
{
	public sealed class PostgresDbContext : DbContext
	{
		public DbSet<MessageEnvelope> MessageEnvelopes { get; set; }
		public DbSet<TrainServiceView> TrainServices { get; set; }
		public DbSet<VehicleView> Vehicles { get; set; }
		public DbSet<ServiceVehicleView> ServiceVehicles { get; set; }

		public PostgresDbContext(DbContextOptions<PostgresDbContext> options) : base(options)
		{
		}

		protected override void OnModelCreating(ModelBuilder modelBuilder)
		{
			var env = modelBuilder.Entity<MessageEnvelope>();
			env.ToTable("message_envelopes");
			env.HasKey(x => x.Id);
			env.Property(x => x.Id).HasColumnName("id");
			env.Property(x => x.ReceivedAt).HasColumnName("received_at").HasColumnType("timestamp with time zone");
			env.Property(x => x.Payload).HasColumnName("payload").HasColumnType("jsonb");
			// Consider GIN index on payload if needed; created via migration

			// Keyless view mapping for materialized view
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

			// Vehicle MV mapping
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

			// Service-instance vehicles MV mapping
			var sv = modelBuilder.Entity<ServiceVehicleView>();
			sv.HasNoKey();
			sv.ToView("service_vehicle_current");
			sv.Property(x => x.OperationalTrainNumber).HasColumnName("OperationalTrainNumber");
			sv.Property(x => x.ServiceDate).HasColumnName("ServiceDate");
			sv.Property(x => x.OriginStd).HasColumnName("OriginStd");
			sv.Property(x => x.VehicleId).HasColumnName("VehicleId");
			sv.Property(x => x.SpecificType).HasColumnName("SpecificType");
			sv.Property(x => x.TypeOfVehicle).HasColumnName("TypeOfVehicle");
			sv.Property(x => x.NumberOfCabs).HasColumnName("NumberOfCabs");
			sv.Property(x => x.NumberOfSeats).HasColumnName("NumberOfSeats");
			sv.Property(x => x.LengthUnit).HasColumnName("LengthUnit");
			sv.Property(x => x.LengthMm).HasColumnName("LengthMm");
			sv.Property(x => x.Weight).HasColumnName("Weight");
			sv.Property(x => x.MaximumSpeed).HasColumnName("MaximumSpeed");
			sv.Property(x => x.TrainBrakeType).HasColumnName("TrainBrakeType");
			sv.Property(x => x.Livery).HasColumnName("Livery");
			sv.Property(x => x.Decor).HasColumnName("Decor");
			sv.Property(x => x.VehicleStatus).HasColumnName("VehicleStatus");
			sv.Property(x => x.RegisteredStatus).HasColumnName("RegisteredStatus");
			sv.Property(x => x.RegisteredCategory).HasColumnName("RegisteredCategory");
			sv.Property(x => x.DateRegistered).HasColumnName("DateRegistered");
			sv.Property(x => x.DateEnteredService).HasColumnName("DateEnteredService");
			sv.Property(x => x.ResourcePosition).HasColumnName("ResourcePosition");
			sv.Property(x => x.PlannedResourceGroup).HasColumnName("PlannedResourceGroup");
			sv.Property(x => x.ResourceGroupId).HasColumnName("ResourceGroupId");
			sv.Property(x => x.FleetId).HasColumnName("FleetId");
			sv.Property(x => x.TypeOfResource).HasColumnName("TypeOfResource");
			sv.Property(x => x.IsLocomotive).HasColumnName("IsLocomotive");
			sv.Property(x => x.ClassCode).HasColumnName("ClassCode");
			sv.Property(x => x.LastUpdatedAt).HasColumnName("LastUpdatedAt");
		}
	}
}


