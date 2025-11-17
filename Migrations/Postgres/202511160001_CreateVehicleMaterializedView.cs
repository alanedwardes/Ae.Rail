using Ae.Rail.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Ae.Rail.Migrations.Postgres
{
	[DbContext(typeof(PostgresDbContext))]
	[Migration("202511160001_CreateVehicleMaterializedView")]
	public partial class _202511160001_CreateVehicleMaterializedView : Migration
	{
		protected override void Up(MigrationBuilder migrationBuilder)
		{
			// Drop dependent alias then MV
			migrationBuilder.Sql(@"drop view if exists vehicle_current;");
			migrationBuilder.Sql(@"drop materialized view if exists vehicle_v1 cascade;");

			migrationBuilder.Sql(@"
				create materialized view vehicle_v1 as
				select distinct on (""VehicleId"")
				  (veh->>'VehicleId') as ""VehicleId"",
				  (veh->>'SpecificType') as ""SpecificType"",
				  (veh->>'TypeOfVehicle') as ""TypeOfVehicle"",
				  nullif(veh->>'Cabs','')::int as ""NumberOfCabs"",
				  nullif(veh->>'NumberOfSeats','')::int as ""NumberOfSeats"",
				  (veh->'Length'->>'Unit') as ""LengthUnit"",
				  nullif(veh->'Length'->>'Value','')::int as ""LengthMm"",
				  nullif(veh->>'Weight','')::int as ""Weight"",
				  nullif(veh->>'MaximumSpeed','')::int as ""MaximumSpeed"",
				  (veh->>'TrainBrakeType') as ""TrainBrakeType"",
				  (veh->>'Livery') as ""Livery"",
				  (veh->>'Decor') as ""Decor"",
				  (veh->>'VehicleStatus') as ""VehicleStatus"",
				  (veh->>'RegisteredStatus') as ""RegisteredStatus"",
				  (veh->>'RegisteredCategory') as ""RegisteredCategory"",
				  nullif(veh->>'DateRegistered','')::timestamp as ""DateRegistered"",
				  nullif(veh->>'DateEnteredService','')::timestamp as ""DateEnteredService"",
				  nullif(veh->>'ResourcePosition','')::int as ""ResourcePosition"",
				  (veh->>'PlannedResourceGroup') as ""PlannedResourceGroup"",
				  (a->'ResourceGroup'->>'ResourceGroupId') as ""ResourceGroupId"",
				  (a->'ResourceGroup'->>'FleetId') as ""FleetId"",
				  (a->'ResourceGroup'->>'TypeOfResource') as ""TypeOfResource"",
				  case when (a->'ResourceGroup'->>'TypeOfResource') ilike 'L' then true else false end as ""IsLocomotive"",
				  case
					when (a->'ResourceGroup'->>'TypeOfResource') ilike 'U' then substring((a->'ResourceGroup'->>'ResourceGroupId') from 1 for 3)
					else substring((veh->>'VehicleId') from 1 for 2)
				  end as ""ClassCode"",
				  case
					when (cls.first_cls)::int between 1 and 70 then 'Diesel'
					when (cls.first_cls)::int between 71 and 96 then 'Electric'
					when (cls.first_cls)::int = 97 then 'Diesel'
					when (cls.first_cls)::int = 98 then 'Steam'
					when (cls.first_cls)::int between 101 and 299 then 'Diesel'
					when (cls.first_cls)::int between 300 and 398 then 'Electric'
					when (cls.first_cls)::int = 399 then 'Diesel'
					when (cls.first_cls)::int between 400 and 799 then 'Electric'
					when (cls.first_cls)::int = 800 then 'Diesel/Electric (bi-mode)'
					when (cls.first_cls)::int = 801 then 'Electric'
					when (cls.first_cls)::int = 802 then 'Diesel/Electric (bi-mode)'
					when (cls.first_cls)::int = 901 then 'Diesel'
					when (cls.first_cls)::int between 910 and 939 then 'Electric'
					when (cls.first_cls)::int between 950 and 999 then 'Diesel'
					else 'Unknown'
				  end as ""PowerType"",
				  case when nullif(veh->>'Cabs','')::int > 0 then true else false end as ""IsDrivingVehicle"",
				  now() as ""LastUpdatedAt""
				from message_envelopes me
				-- Unnest allocations then vehicles
				join lateral jsonb_array_elements(me.payload->'Allocation') as a on true
				join lateral jsonb_array_elements(coalesce(a->'ResourceGroup'->'Vehicle','[]'::jsonb)) as veh on true
				-- Determine first class code per vehicle row for power type mapping
				left join lateral (
					select case
						when (a->'ResourceGroup'->>'TypeOfResource') ilike 'U' then substring((a->'ResourceGroup'->>'ResourceGroupId') from 1 for 3)
						else substring((veh->>'VehicleId') from 1 for 2)
					end as first_cls
				) cls on true
				order by ""VehicleId"", me.received_at desc;
			");

			// Indexes to support CONCURRENT refresh and lookups
			migrationBuilder.Sql(@"create unique index if not exists ux_vehicle_v1_vehicle_id on vehicle_v1 (""VehicleId"");");
			migrationBuilder.Sql(@"create index if not exists ix_vehicle_v1_class on vehicle_v1 (""ClassCode"");");
			migrationBuilder.Sql(@"create index if not exists ix_vehicle_v1_loco on vehicle_v1 (""IsLocomotive"");");

			// Stable alias view
			migrationBuilder.Sql(@"create or replace view vehicle_current as select * from vehicle_v1;");
		}

		protected override void Down(MigrationBuilder migrationBuilder)
		{
			migrationBuilder.Sql(@"drop view if exists vehicle_current;");
			migrationBuilder.Sql(@"drop materialized view if exists vehicle_v1 cascade;");
		}
	}
}


