﻿using System;
using Ether.Components;
using Ether.Redux.Interfaces;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.RenderTree;

namespace Ether.Redux.Blazor
{
    public class ComponentConnected<TComponent, TState, TProps> : ComponentBase, IDisposable
        where TComponent : ComponentBase
        where TProps : new()
    {
        private TProps _props;

        [Inject]
        protected IStore<TState> Store { get; set; }

        [Parameter]
        protected Action<TState, TProps> MapStateToProps { get; set; }

        [Parameter]
        protected Action<IStore<TState>, TProps> MapDispatchToProps { get; set; }

        public void Dispose()
        {
            Store.OnStateChanged -= OnStateChanged;
        }

        protected override void OnInit()
        {
            Store.OnStateChanged += OnStateChanged;
            Console.WriteLine($"[ComponentConnected] - {nameof(OnInitAsync)}");
        }

        protected override void OnParametersSet()
        {
            base.OnParametersSet();

            if (MapStateToProps == null || MapDispatchToProps == null)
            {
                throw new ArgumentNullException($"Connect requires both {nameof(MapStateToProps)} and ${nameof(MapDispatchToProps)} to be set.");
            }
        }

        protected override void BuildRenderTree(RenderTreeBuilder builder)
        {
            base.BuildRenderTree(builder);

            InitializeProps();

            Console.WriteLine($"[ComponentConnected] - Rendering JobLogs");
            builder.OpenComponent<TComponent>(1);
            builder.AddAttribute(2, "Props", _props);
            builder.CloseComponent();
        }

        private void OnStateChanged(object sender, EventArgs e)
        {
            Console.WriteLine($"[ComponentConnected] - {nameof(OnStateChanged)}");
            InitializeProps();
            MapStateToProps(Store.State, _props);
            this.StateHasChanged();
        }

        private void InitializeProps()
        {
            if (_props == null)
            {
                Console.WriteLine($"[ComponentConnected] - {nameof(InitializeProps)}");
                _props = new TProps();
                MapDispatchToProps(Store, _props);
            }
        }
    }
}
