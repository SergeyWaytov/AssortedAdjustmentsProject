using Base.Core;
using Base.Defs;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace SergeyWaytov.AssortedAdjustmentsProject
{
    public class DefCache
    {
        private readonly DefRepository _repo;
        private readonly Dictionary<string, List<string>> _defNameToGuidCache;

        // Static instance for global access (used by TutorialPatches)
        public static DefCache Instance { get; private set; }

        public DefCache()
        {
            _repo = GameUtl.GameComponent<DefRepository>();
            _defNameToGuidCache = new Dictionary<string, List<string>>();

            foreach (BaseDef baseDef in _repo.DefRepositoryDef.AllDefs)
            {
                AddDef(baseDef.name, baseDef.Guid);
            }

            // Set static instance
            Instance = this;
        }

        public T GetDef<T>(string name) where T : BaseDef
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                    return null;

                if (!_defNameToGuidCache.TryGetValue(name, out List<string> guids) || guids == null || guids.Count == 0)
                    return null;

                foreach (string guid in guids)
                {
                    if (string.IsNullOrEmpty(guid))
                        continue;

                    BaseDef def = _repo.GetDef(guid);
                    if (def is T typed)
                        return typed;
                }

                return null;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DefCache] Error retrieving def '{name}': {e.Message}");
                return null;
            }
        }

        public List<T> GetDefs<T>(string name) where T : BaseDef
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                    return null;

                if (!_defNameToGuidCache.TryGetValue(name, out List<string> guids) || guids == null)
                    return null;

                List<T> result = new List<T>();
                foreach (string guid in guids)
                {
                    BaseDef def = string.IsNullOrEmpty(guid) ? null : _repo.GetDef(guid);
                    if (def is T typed)
                        result.Add(typed);
                }
                return result;
            }
            catch (Exception e)
            {
                Debug.LogError($"[DefCache] Error retrieving defs for '{name}': {e.Message}");
                return null;
            }
        }

        private void AddDef(string name, string guid)
        {
            if (_defNameToGuidCache.ContainsKey(name))
            {
                if (!_defNameToGuidCache[name].Contains(guid))
                    _defNameToGuidCache[name].Add(guid);
            }
            else
            {
                _defNameToGuidCache.Add(name, new List<string> { guid });
            }
        }
    }
}