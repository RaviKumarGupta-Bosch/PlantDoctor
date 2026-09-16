using System.ServiceModel;

namespace PlantDoctor.Contracts;

[ServiceContract(Namespace = "http://plantdoctor.local/plant-monitor")]
public interface IPlantMonitorService
{
    [OperationContract(IsOneWay = true)]
    void ReportSensorReading(SensorReadingDto dto);

    [OperationContract(IsOneWay = true)]
    void ReportError(ErrorEventDto dto);

    [OperationContract(IsOneWay = true)]
    void ReportComEvent(ComEventDto dto);

    [OperationContract(IsOneWay = true)]
    void ReportScanEvent(ScanEventDto dto);

    [OperationContract]
    PlantSnapshotDto GetCurrentSnapshot();
}
