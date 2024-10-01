namespace DependencyUpdated.Repositories.AzureDevOps.Dto;

public record ApproveBody(int Vote)
{
    public static ApproveBody Approve()
    {
        return new ApproveBody(10);
    }
}