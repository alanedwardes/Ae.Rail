using Ae.Rail.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Ae.Rail.Migrations.Postgres
{
	[DbContext(typeof(PostgresDbContext))]
	[Migration("202511170002_CreateRegularTables")]
	public partial class _202511170002_CreateRegularTables : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Create train_services table
			migrationBuilder.Sql(@"
				create table if not exists train_services (
					id bigserial primary key,
					operational_train_number varchar(10),
					service_date varchar(10),
					origin_std varchar(5),
					train_origin_datetime timestamptz,
					train_dest_datetime timestamptz,
					origin_location_primary_code varchar(10),
					origin_location_name varchar(100),
					dest_location_primary_code varchar(10),
					dest_location_name varchar(100),
					fleet_id varchar(50),
					type_of_resource varchar(10),
					resource_group_id varchar(50),
					class_code varchar(10),
					power_type varchar(50),
					rail_classes varchar(50),
					toi_core varchar(20),
					toi_variant varchar(10),
					toi_timetable_year int,
					toi_start_date date,
					created_at timestamptz not null default now(),
					updated_at timestamptz not null default now()
				);
			");

			// Indexes for train_services
			migrationBuilder.Sql(@"create unique index if not exists ux_train_services_key on train_services (operational_train_number, service_date, origin_std, train_origin_datetime);");
			migrationBuilder.Sql(@"create index if not exists ix_train_services_otn on train_services (operational_train_number);");
			migrationBuilder.Sql(@"create index if not exists ix_train_services_service_date on train_services (service_date);");
			migrationBuilder.Sql(@"create index if not exists ix_train_services_origin_dest on train_services (origin_location_primary_code, dest_location_primary_code);");
			migrationBuilder.Sql(@"create index if not exists ix_train_services_origin_dest_name on train_services (origin_location_name, dest_location_name);");

			// Create vehicles table
			migrationBuilder.Sql(@"
				create table if not exists vehicles (
					id bigserial primary key,
					vehicle_id varchar(20) not null,
					specific_type varchar(50),
					type_of_vehicle varchar(50),
					number_of_cabs int,
					number_of_seats int,
					length_unit varchar(10),
					length_mm int,
					weight int,
					maximum_speed int,
					train_brake_type varchar(50),
					livery varchar(100),
					decor varchar(100),
					vehicle_status varchar(50),
					registered_status varchar(50),
					registered_category varchar(50),
					date_registered timestamptz,
					date_entered_service timestamptz,
					resource_position int,
					planned_resource_group varchar(50),
					resource_group_id varchar(50),
					fleet_id varchar(50),
					type_of_resource varchar(10),
					is_locomotive boolean not null default false,
					class_code varchar(10),
					power_type varchar(50),
					is_driving_vehicle boolean not null default false,
					created_at timestamptz not null default now(),
					updated_at timestamptz not null default now()
				);
			");

			// Indexes for vehicles
			migrationBuilder.Sql(@"create unique index if not exists ux_vehicles_vehicle_id on vehicles (vehicle_id);");
			migrationBuilder.Sql(@"create index if not exists ix_vehicles_class_code on vehicles (class_code);");
			migrationBuilder.Sql(@"create index if not exists ix_vehicles_specific_type on vehicles (specific_type);");
			migrationBuilder.Sql(@"create index if not exists ix_vehicles_is_locomotive on vehicles (is_locomotive);");

			// Create service_vehicles table
			migrationBuilder.Sql(@"
				create table if not exists service_vehicles (
					id bigserial primary key,
					operational_train_number varchar(10) not null,
					service_date varchar(10) not null,
					origin_std varchar(5) not null,
					vehicle_id varchar(20) not null,
					specific_type varchar(50),
					type_of_vehicle varchar(50),
					number_of_cabs int,
					number_of_seats int,
					length_unit varchar(10),
					length_mm int,
					weight int,
					maximum_speed int,
					train_brake_type varchar(50),
					livery varchar(100),
					decor varchar(100),
					vehicle_status varchar(50),
					registered_status varchar(50),
					registered_category varchar(50),
					date_registered timestamptz,
					date_entered_service timestamptz,
					resource_position int,
					planned_resource_group varchar(50),
					resource_group_id varchar(50),
					fleet_id varchar(50),
					type_of_resource varchar(10),
					is_locomotive boolean not null default false,
					class_code varchar(10),
					created_at timestamptz not null default now(),
					updated_at timestamptz not null default now()
				);
			");

			// Indexes for service_vehicles
			migrationBuilder.Sql(@"create unique index if not exists ux_service_vehicles_key on service_vehicles (operational_train_number, service_date, origin_std, vehicle_id);");
			migrationBuilder.Sql(@"create index if not exists ix_service_vehicles_service on service_vehicles (operational_train_number, service_date, origin_std);");
			migrationBuilder.Sql(@"create index if not exists ix_service_vehicles_vehicle_id on service_vehicles (vehicle_id);");
			migrationBuilder.Sql(@"create index if not exists ix_service_vehicles_resource_position on service_vehicles (operational_train_number, service_date, origin_std, resource_position);");
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(@"drop table if exists service_vehicles;");
			migrationBuilder.Sql(@"drop table if exists vehicles;");
			migrationBuilder.Sql(@"drop table if exists train_services;");
		}
	}
}

