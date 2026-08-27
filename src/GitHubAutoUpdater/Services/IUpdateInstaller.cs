using System.Threading.Tasks;
using GitHubAutoUpdater.Models;

namespace GitHubAutoUpdater.Services
{
    public interface IUpdateInstaller
    {
        Task ApplyUpdateAndRestartAsync(string packagePath, UpdateApplyOptions? options = null);
    }
}
