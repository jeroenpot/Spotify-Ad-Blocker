using System.Threading.Tasks;

namespace EZBlocker
{
    public interface IWebRepository
    {
        Task<T> GetData<T>(string path);
        Task<string> GetData(string path);
    }
}