using System.Text.Json.Serialization;

namespace CellphoneProcessor.Utilities.OTP;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public sealed class GetPlanResponse
{
    public TripPlan? Plan { get; set; }

    public Dictionary<string, string>? RequestParameters { get; set; }

    public Dictionary<string, object>? DebugOutput { get; set; }

    public TripPlanError? Error
    {
        get;
        set;
    }

    public bool ServerError => Error?.Message == "SYSTEM_ERROR";

    public TransitLoS GetLoS()
    {
        if (Error?.Message == "PATH_NOT_FOUND")
        {
            if(Error?.Msg == "No trip found. There may be no transit service within the maximum specified distance or at the specified time, or your start or end point might not be safely accessible.")
            {
                return TransitLoS.GetBadRequest();
            }
            else
            {
                throw new Exception(Error?.Msg);
            }
        }
        else if (Error?.Message == "TOO_CLOSE")
        {
            return TransitLoS.GetIntrazonal();
        }
        else if(Plan is TripPlan plan)
        {
             return plan.GetLoS();
        }
        else
        {
            return TransitLoS.GetBadRequest();
        }
    }
}

