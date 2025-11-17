using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using System.Xml.Serialization;

namespace Ae.Rail.Models.TafTsi
{
    [XmlRoot("PassengerTrainConsistMessage", Namespace = "http://www.era.europa.eu/schemes/TAFTSI/5.3")]
    public class PassengerTrainConsistMessage
    {
        [XmlElement("MessageHeader")]
        public MessageHeader MessageHeader { get; set; }

        [XmlElement("MessageStatus")]
        public int? MessageStatus { get; set; }

        [XmlElement("TrainOperationalIdentification")]
        public TrainOperationalIdentification TrainOperationalIdentification { get; set; }

        [XmlElement("OperationalTrainNumberIdentifier")]
        public OperationalTrainNumberIdentifier OperationalTrainNumberIdentifier { get; set; }

        [XmlElement("ResponsibleRU")]
        public string ResponsibleRU { get; set; }

        [XmlElement("Allocation")]
        public List<Allocation> Allocation { get; set; }
    }

    public class MessageHeader
    {
        [XmlElement("MessageReference")]
        public MessageReference MessageReference { get; set; }

        [XmlElement("Sender")]
        public string Sender { get; set; }

        [XmlElement("Recipient")]
        public string Recipient { get; set; }
    }

    public class MessageReference
    {
        [XmlElement("MessageType")]
        public string MessageType { get; set; }

        [XmlElement("MessageTypeVersion")]
        public string MessageTypeVersion { get; set; }

        [XmlElement("MessageIdentifier")]
        public string MessageIdentifier { get; set; }

        [XmlElement("MessageDateTime")]
        public DateTime? MessageDateTime { get; set; }
    }

    public class TrainOperationalIdentification
    {
        [XmlElement("TransportOperationalIdentifiers")]
        public List<TransportOperationalIdentifiers> TransportOperationalIdentifiers { get; set; }
    }

    public class TransportOperationalIdentifiers
    {
        [XmlElement("ObjectType")]
        public string ObjectType { get; set; }

        [XmlElement("Company")]
        public string Company { get; set; }

        [XmlElement("Core")]
        public string Core { get; set; }

        [XmlElement("Variant")]
        public string Variant { get; set; }

        [XmlElement("TimetableYear")]
        public int? TimetableYear { get; set; }

        [XmlElement("StartDate")]
        public DateTime? StartDate { get; set; }
    }

    public class OperationalTrainNumberIdentifier
    {
        [XmlElement("OperationalTrainNumber")]
        public string OperationalTrainNumber { get; set; }

        [XmlElement("ScheduledTimeAtHandover")]
        public DateTime? ScheduledTimeAtHandover { get; set; }

        [XmlElement("ScheduledDateTimeAtTransfer")]
        public DateTime? ScheduledDateTimeAtTransfer { get; set; }
    }

    public class Allocation
    {
        [XmlElement("AllocationSequenceNumber")]
        public int? AllocationSequenceNumber { get; set; }

        [XmlElement("TrainOriginDateTime")]
        public DateTime? TrainOriginDateTime { get; set; }

        [XmlElement("TrainOriginLocation")]
        public Location TrainOriginLocation { get; set; }

        [XmlElement("ResourceGroupPosition")]
        public int? ResourceGroupPosition { get; set; }

        [XmlElement("DiagramDate")]
        public DateTime? DiagramDate { get; set; }

        [XmlElement("DiagramNo")]
        public string DiagramNo { get; set; }

        [XmlElement("TrainDestLocation")]
        public Location TrainDestLocation { get; set; }

        [XmlElement("TrainDestDateTime")]
        public DateTime? TrainDestDateTime { get; set; }

        [XmlElement("AllocationOriginLocation")]
        public Location AllocationOriginLocation { get; set; }

        [XmlElement("AllocationOriginDateTime")]
        public DateTime? AllocationOriginDateTime { get; set; }

        [XmlElement("AllocationOriginMiles")]
        public int? AllocationOriginMiles { get; set; }

        [XmlElement("AllocationDestinationLocation")]
        public Location AllocationDestinationLocation { get; set; }

        [XmlElement("AllocationDestinationDateTime")]
        public DateTime? AllocationDestinationDateTime { get; set; }

        [XmlElement("AllocationDestinationMiles")]
        public int? AllocationDestinationMiles { get; set; }

        [XmlElement("Reversed")]
        public string Reversed { get; set; }

        [XmlElement("ResourceGroup")]
        public ResourceGroup ResourceGroup { get; set; }
    }

    public class Location
    {
        [XmlElement("CountryCodeISO")]
        public string CountryCodeISO { get; set; }

        [XmlElement("LocationPrimaryCode")]
        public string LocationPrimaryCode { get; set; }

        [XmlElement("LocationSubsidiaryIdentification")]
        public LocationSubsidiaryIdentification LocationSubsidiaryIdentification { get; set; }
    }

    public class LocationSubsidiaryIdentification
    {
        [XmlElement("LocationSubsidiaryCode")]
        public string LocationSubsidiaryCode { get; set; }

        [XmlElement("AllocationCompany")]
        public string AllocationCompany { get; set; }
    }

    public class ResourceGroup
    {
        [XmlElement("ResourceGroupId")]
        public string ResourceGroupId { get; set; }

        [XmlElement("TypeOfResource")]
        public string TypeOfResource { get; set; }

        [XmlElement("FleetId")]
        public string FleetId { get; set; }

        [XmlElement("ResourceGroupStatus")]
        public string ResourceGroupStatus { get; set; }

        [XmlElement("EndOfDayMiles")]
        public long? EndOfDayMiles { get; set; }

        [XmlElement("Preassignment")]
        public Preassignment Preassignment { get; set; }

        [XmlElement("Vehicle")]
        public List<Vehicle> Vehicle { get; set; }
    }

    public class Preassignment
    {
        [XmlElement("PreAssignmentRequiredLocation")]
        public Location PreAssignmentRequiredLocation { get; set; }

        [XmlElement("PreAssignmentDueDateTime")]
        public DateTime? PreAssignmentDueDateTime { get; set; }

        [XmlElement("PreAssignmentReason")]
        public string PreAssignmentReason { get; set; }

        [XmlElement("PreAssignmentDateTime")]
        public DateTime? PreAssignmentDateTime { get; set; }
    }

    public class Vehicle
    {
        [XmlElement("VehicleId")]
        public string VehicleId { get; set; }

        [XmlElement("TypeOfVehicle")]
        public string TypeOfVehicle { get; set; }

        [XmlElement("ResourcePosition")]
        public int? ResourcePosition { get; set; }

        [XmlElement("PlannedResourceGroup")]
        public string PlannedResourceGroup { get; set; }

        [XmlElement("SpecificType")]
        public string SpecificType { get; set; }

        [XmlElement("Length")]
        public Measure Length { get; set; }

        [XmlElement("Weight")]
        public int? Weight { get; set; }

        [XmlElement("Livery")]
        public string Livery { get; set; }

        [XmlElement("Decor")]
		public string? Decor { get; set; }

        [XmlElement("SpecialCharacteristics")]
        public string SpecialCharacteristics { get; set; }

        [XmlElement("NumberOfSeats")]
        public int? NumberOfSeats { get; set; }

        [XmlElement("VehicleStatus")]
        public string VehicleStatus { get; set; }

        [XmlElement("RegisteredStatus")]
        public string RegisteredStatus { get; set; }

        [XmlElement("Cabs")]
        public int? Cabs { get; set; }

        [XmlElement("DateEnteredService")]
        public DateTime? DateEnteredService { get; set; }

        [XmlElement("DateRegistered")]
        public DateTime? DateRegistered { get; set; }

        [XmlElement("RegisteredCategory")]
        public string RegisteredCategory { get; set; }

        [XmlElement("TrainBrakeType")]
        public string TrainBrakeType { get; set; }

        [XmlElement("MaximumSpeed")]
        public int? MaximumSpeed { get; set; }

        [XmlElement("Defect")]
        public List<Defect> Defect { get; set; }
    }

    public class Measure
    {
        [XmlElement("Value")]
        public decimal? Value { get; set; }

        [XmlElement("Measure")]
        public string Unit { get; set; }
    }

    public class Defect
    {
        [XmlElement("MaintenanceUID")]
        public string MaintenanceUID { get; set; }

        [XmlElement("DefectCode")]
        public string DefectCode { get; set; }

        [XmlElement("DefectDescription")]
        public string DefectDescription { get; set; }

        [XmlElement("DefectStatus")]
        public string DefectStatus { get; set; }
    }
}


