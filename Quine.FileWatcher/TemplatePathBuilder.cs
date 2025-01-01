#if false
using System;
using System.Collections.Generic;
using Quine.HRCatalog;

namespace Quine.Ingest.Nucleus.Fs;

/// <summary>
/// Contains predefines methods that interpret and fill in template path variables.
/// </summary>
public static class TemplatePathBuilder
{

    public static IReadOnlyDictionary<string,string> IngestJobVariables = new Dictionary<string,string> {
        { "CatalogDate", null },
        { "DeviceManufacturer", null },
        { "DeviceModel", null }
    };
    public static IReadOnlyDictionary<string, string> GeneratedFileVariables = new Dictionary<string, string> {
        { "LogicalResourceDate", null },
        { "PhysicalResourceDate", null },
        { "Purpose", null },
        { "DeviceType", null },
        { "DeviceManufacturer", null },
        { "DeviceModel", null },
    };

    /// MEDIA/{DeviceManufacturer}/{CatalogDate}
    /// <summary>
    /// {CatalogDate}              Job.IngestJob.CatalogDate               IngestJob date. This will be the same date for all files of an IngestJob. The date the user specifies on ingest start
    /// {DeviceManufacturer}       Physical.Device.Manufacturer            Manufacturer name as specified in production device by user during ingest start.
    /// {DeviceModel}              Physical.Device.Model                   Model name as specified in production device by user during ingest start
    /// </summary>
    public static Schemas.Core.PathComponents CreateIngestJobProjectPath
        (
        string templatePath,
        string deviceManufacturer,
        string deviceModel,
        DateTime? catalogDate
        )
    {
        QHEnsure.NotEmpty(templatePath);
        var args = new Dictionary<string, string>(IngestJobVariables);
        
        args["CatalogDate"] = catalogDate?.ToString("yyyy-MM-dd");
        args["DeviceManufacturer"] = deviceManufacturer;
        args["DeviceModel"] = deviceModel;

        return CreateProjectPath(templatePath, args);
    }

    /// MEDIA/KLIPPEFILER/{LogicalResourceDate}
    /// <summary>
    /// {LogicalResourceDate}      Logical.Resource.CreationTime           The best known origin creation date of a clip or other, similar, collection of physical resources.                 
    /// {PhysicalResourceDate}     Physical.Resource.CreationTime          Date the specific resource was created
    /// {Purpose}                  Physical.Resource.Purpose               The purpose of each individual physical resource. 
    /// {DeviceType}               Physical.Device.Type
    /// {DeviceManufacturer}       Physical.Device.Manufacturer
    /// {DeviceModel}              Physical.Device.Model
    /// </summary>
    /// <param name="ingestJob"></param>
    /// <returns></returns>

    [Obsolete("Using device properties without DeviceManager.GetInheritedProperty", true)]
    public static Schemas.Core.PathComponents CreateFileProjectPath(
        string templatePath, Schemas.Db.PhysicalDevice device,
        Schemas.Db.LogicalResource lr, Schemas.Db.PhysicalResource pr) {

        // Devices used for IngestJobs should not be sent to this function, and
        // there is no reason for a device NOT used by IngestJobs to have IngestTemplatePath.
        //System.Diagnostics.Debug.Assert(device.Augmented?.IngestTemplatePath == null);

        var args = new Dictionary<string, string>(GeneratedFileVariables);
        args["LogicalResourceDate"] = lr.CreationTime.ToString("yyyy-MM-dd");
        args["PhysicalResourceDate"] = pr.CreationTime.ToString("yyyy-MM-dd");
        args["Purpose"] = pr.Purpose;
        args["DeviceType"] = device?.Type;
        args["DeviceManufacturer"] = device?.Manufacturer;
        args["DeviceModel"] = device?.Model;
        
        return CreateProjectPath(templatePath, args);
    }

    public static Schemas.Core.PathComponents CreateFileProjectPath(
        string templatePath, DateTime? lrDate, DateTime? prDate, string purpose, string deviceType, string manufacturer, string model) {
        var args = new Dictionary<string, string>() {
            {"LogicalResourceDate", lrDate?.ToString("yyyy-MM-dd") },
            {"PhysicalResourceDate", prDate?.ToString("yyyy-MM-dd") },
            {"Purpose", purpose },
            {"DeviceType", deviceType },
            {"DeviceManufacturer", manufacturer },
            {"DeviceModel", model },
        };

        return CreateProjectPath(templatePath, args);
    }

    static Schemas.Core.PathComponents CreateProjectPath(string templateString, Dictionary<string,string> templateArguments) {
        templateString = Schemas.Core.TemplateVariableProcessor.Replace(templateString, ArgumentValue);
        return Schemas.Core.PathComponents.Make(templateString);
        string ArgumentValue(string name) {
            if (!templateArguments.TryGetValue(name, out var v) || v == null)
                v = $"UNKNOWN_{name}";
            return v;
        }
    }
}
#endif