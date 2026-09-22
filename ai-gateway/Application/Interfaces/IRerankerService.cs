namespace AiGateway.Application.Interfaces
{
    public interface IRerankerService
    {
        Task<float> ComputeScoreAsync(string query, string passage);
    }
}