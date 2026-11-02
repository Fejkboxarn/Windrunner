using System;
using System.Collections.Generic;
using UnityEngine;

namespace Windrunner.Production.Feedback
{
    [Serializable]
    public sealed class FeedbackCatalogItem
    {
        public FeedbackKey key;
        public FeedbackEventDefinition definition;
    }

    [CreateAssetMenu(menuName = "Windrunner/Feedback/Catalog")]
    public sealed class FeedbackCatalog : ScriptableObject
    {
        [SerializeField] private FeedbackCatalogItem[] entries;
        public IReadOnlyList<FeedbackCatalogItem> Entries => entries;
        public void Configure(FeedbackCatalogItem[] items) => entries = items;
    }
}
