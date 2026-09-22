using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Ceres.Pool;
using Ceres.Resource;
using Cysharp.Threading.Tasks;

namespace Ceres.DataDriven
{
    public abstract class DataTableManager
    {
        private static readonly object InitializationGate = new();
        private static readonly Dictionary<Type, DataTableManager> DataTableManagers = new();
        private static readonly HashSet<Type> InitializedManagerTypes = new();
        private static UniTaskCompletionSource _initializationCompletion;
        private static int _initializationGeneration;
        
        public static void Initialize()
        {
            (Type Type, DataTableManager Manager)[] pending;
            UniTaskCompletionSource completion;
            int generation;
            lock (InitializationGate)
            {
                Type[] managerTypes = DiscoverManagerTypes();
                RegisterMissingManagers(managerTypes);
                pending = GetPendingManagers(managerTypes);
                if (pending.Length == 0) return;
                if (_initializationCompletion != null)
                {
                    throw new InvalidOperationException(
                        "DataTable managers are initializing asynchronously. Await DataTableManager.InitializeAsync() before using Get().");
                }

                completion = new UniTaskCompletionSource();
                _initializationCompletion = completion;
                generation = _initializationGeneration;
            }

            try
            {
                foreach ((Type type, DataTableManager manager) in pending)
                {
                    manager.Initialize(true).GetAwaiter().GetResult();
                    MarkManagerInitialized(type, generation);
                }
                CompleteInitialization(completion, generation);
            }
            catch (Exception exception)
            {
                FailInitialization(completion, generation, exception);
                throw;
            }
        }
        
        /// <summary>
        /// Manual initialization api
        /// </summary>
        /// <returns></returns>
        public static UniTask InitializeAsync()
        {
            (Type Type, DataTableManager Manager)[] pending;
            UniTaskCompletionSource completion;
            int generation;
            lock (InitializationGate)
            {
                Type[] managerTypes = DiscoverManagerTypes();
                RegisterMissingManagers(managerTypes);
                pending = GetPendingManagers(managerTypes);
                if (pending.Length == 0) return UniTask.CompletedTask;
                if (_initializationCompletion != null)
                    return AwaitInitializationAndRescan(_initializationCompletion.Task);

                completion = new UniTaskCompletionSource();
                _initializationCompletion = completion;
                generation = _initializationGeneration;
            }

            InitializeManagersAsync(pending, completion, generation).Forget();
            return completion.Task;
        }

        private static async UniTask AwaitInitializationAndRescan(UniTask initialization)
        {
            await initialization;
            await InitializeAsync();
        }

        private static async UniTask InitializeManagersAsync(
            IEnumerable<(Type Type, DataTableManager Manager)> managers,
            UniTaskCompletionSource completion,
            int generation)
        {
            try
            {
                using var parallel = UniParallel.Get();
                foreach ((Type type, DataTableManager manager) in managers)
                    parallel.Add(InitializeManagerAsync(type, manager, generation));
                await parallel;
                CompleteInitialization(completion, generation);
            }
            catch (Exception exception)
            {
                FailInitialization(completion, generation, exception);
            }
        }

        private static async UniTask InitializeManagerAsync(
            Type type,
            DataTableManager manager,
            int generation)
        {
            await manager.Initialize(false);
            MarkManagerInitialized(type, generation);
        }

        private static Type[] DiscoverManagerTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(assembly => assembly.GetTypes())
                .Where(type => typeof(DataTableManager).IsAssignableFrom(type) && !type.IsAbstract)
                .ToArray();
        }

        private static void RegisterMissingManagers(IEnumerable<Type> managerTypes)
        {
            object[] args = { null };
            foreach (Type type in managerTypes)
            {
                if (DataTableManagers.ContainsKey(type)) continue;
                _ = (DataTableManager)Activator.CreateInstance(type, args);
                if (!DataTableManagers.ContainsKey(type))
                    throw new InvalidOperationException($"DataTable manager '{type.FullName}' did not register itself.");
            }
        }

        private static (Type Type, DataTableManager Manager)[] GetPendingManagers(
            IEnumerable<Type> managerTypes)
        {
            return managerTypes
                .Where(type => !InitializedManagerTypes.Contains(type))
                .Select(type => (type, DataTableManagers[type]))
                .ToArray();
        }

        private static void MarkManagerInitialized(Type type, int generation)
        {
            lock (InitializationGate)
            {
                if (generation != _initializationGeneration)
                    throw new OperationCanceledException("DataTable manager initialization was invalidated.");
                InitializedManagerTypes.Add(type);
            }
        }

        private static void CompleteInitialization(
            UniTaskCompletionSource completion,
            int generation)
        {
            bool current;
            lock (InitializationGate)
            {
                current = generation == _initializationGeneration &&
                          ReferenceEquals(_initializationCompletion, completion);
                if (current)
                    _initializationCompletion = null;
            }

            if (current)
                completion.TrySetResult();
            else
                completion.TrySetCanceled();
        }

        private static void FailInitialization(
            UniTaskCompletionSource completion,
            int generation,
            Exception exception)
        {
            bool current;
            lock (InitializationGate)
            {
                current = generation == _initializationGeneration &&
                          ReferenceEquals(_initializationCompletion, completion);
                if (current)
                    _initializationCompletion = null;
            }

            if (current)
                completion.TrySetException(exception);
            else
                completion.TrySetCanceled();
        }

        protected readonly Dictionary<string, DataTable> DataTables = new();

        protected void RegisterDataTable(string name, DataTable dataTable)
        {
            DataTables[name] = dataTable;
        }
        
        protected void RegisterDataTable(DataTable dataTable)
        {
            DataTables[dataTable.name] = dataTable;
        }
        
        public DataTable GetDataTable(string name)
        {
            return DataTables.GetValueOrDefault(name);
        }

        /// <summary>
        /// Free all <see cref="DataTableManager"/> for clearing cache
        /// </summary>
        public static void ReleaseAll()
        {
            UniTaskCompletionSource completion;
            lock (InitializationGate)
            {
                _initializationGeneration++;
                completion = _initializationCompletion;
                _initializationCompletion = null;
                InitializedManagerTypes.Clear();
                DataTableManagers.Clear();
            }
            completion?.TrySetCanceled();
        }
        
        /// <summary>
        /// Async initialize manager at start of game, loading your dataTables in this stage
        /// </summary>
        /// <param name="sync">Whether initialize in sync, useful when need blocking loading</param>
        /// <returns></returns>
        protected abstract UniTask Initialize(bool sync);

        /// <summary>
        /// Initialize with loading a single table
        /// </summary>
        /// <param name="tableKey"></param>
        /// <param name="sync"></param>
        protected async UniTask InitializeSingleTable(string tableKey, bool sync)
        {
            try
            {
                if (sync)
                {
                    if (DataDrivenConfig.ValidateDataTableBeforeLoad)
                    {
                        // ReSharper disable once MethodHasAsyncOverload
                        ResourceSystem.EnsureAssetExists<DataTable>(tableKey);
                    }
                    DataTable syncTable = ResourceSystem.LoadAssetAsync<DataTable>(tableKey)
                        .WaitForCompletion();
                    RegisterDataTable(tableKey, syncTable);
                    return;
                }

                if (DataDrivenConfig.ValidateDataTableBeforeLoad)
                {
                    await ResourceSystem.EnsureAssetExistsAsync<DataTable>(tableKey);
                }
                DataTable asyncTable = await ResourceSystem.LoadAssetAsync<DataTable>(tableKey);
                RegisterDataTable(tableKey, asyncTable);
            }
            catch (InvalidResourceRequestException)
            {

            }
        }

        public static DataTableManager GetOrCreateDataTableManager(Type type)
        {
            lock (InitializationGate)
            {
                if (DataTableManagers.TryGetValue(type, out var dataTableManager))
                    return dataTableManager;
            }

            var getMethod = type.GetMethod("Get", BindingFlags.Static | BindingFlags.Public);
            return (DataTableManager)getMethod!.Invoke(null, Array.Empty<object>());
        }
        
        public static bool TryGetDataTableManager(Type type, out DataTableManager dataTableManager)
        {
            lock (InitializationGate)
                return DataTableManagers.TryGetValue(type, out dataTableManager);
        }
        
        public static DataTableManager GetDataTableManager(Type type)
        {
            lock (InitializationGate)
                return DataTableManagers.GetValueOrDefault(type);
        }

        protected static bool TryGetInitializedDataTableManager(
            Type type,
            out DataTableManager dataTableManager)
        {
            lock (InitializationGate)
            {
                dataTableManager = null;
                return InitializedManagerTypes.Contains(type) &&
                       DataTableManagers.TryGetValue(type, out dataTableManager);
            }
        }
        
        protected static void RegisterDataTableManager<TManager>(TManager dataTableManager) where TManager: DataTableManager
        {
            lock (InitializationGate)
                DataTableManagers.TryAdd(typeof(TManager), dataTableManager);
        }
    }
    
    public abstract class DataTableManager<TManager> : DataTableManager where TManager : DataTableManager<TManager>
    {
        private static readonly Type ManagerType;

        static DataTableManager()
        {
            ManagerType = typeof(TManager);
        }
        
        // Force implementation has this constructor
        protected DataTableManager(object _)
        {
            RegisterDataTableManager((TManager)this);
        }
        
        /// <summary>
        /// Get <see cref="DataTableManager{TManager}"/> singleton
        /// </summary>
        /// <returns></returns>
        public static TManager Get()
        {
            if (TryGetInitializedDataTableManager(ManagerType, out var dataTableManager))
            {
                return dataTableManager as TManager;
            }
            Initialize();
            if (TryGetInitializedDataTableManager(ManagerType, out dataTableManager))
                return dataTableManager as TManager;
            throw new InvalidOperationException($"DataTable manager '{ManagerType.FullName}' is not initialized.");
        }
    }
}
