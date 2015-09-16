using System.Threading.Tasks;

namespace NEventStore.Persistence.AcceptanceTests.BDD
{
    public abstract class SpecificationBase
    {
        protected virtual void BecauseSynch() { }

        protected virtual void CleanupSynch() {  }

        protected virtual void EstablishSynch() {  }

        protected virtual Task Because() { return Task.FromResult(false); }

        protected virtual Task Cleanup() { return Task.FromResult(false); }

        protected virtual Task Context() { return Task.FromResult(false); }

        internal Task OnFinish()
        {
            CleanupSynch();
            return Cleanup();
        }

        internal async Task OnStart()
        {
            await Context();
            EstablishSynch();
            await Because();
            BecauseSynch();
        }
    }
}