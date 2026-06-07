import { Routes } from '@angular/router';

export const routes: Routes = [
  {
    path: '',
    pathMatch: 'full',
    redirectTo: 'overview'
  },
  {
    path: 'overview',
    loadComponent: () => import('./features/overview/overview.page').then((m) => m.OverviewPage)
  },
  {
    path: 'accounts',
    loadComponent: () => import('./features/accounts/accounts.page').then((m) => m.AccountsPage)
  },
  {
    path: 'channels',
    loadComponent: () => import('./features/channels/channels.page').then((m) => m.ChannelsPage)
  },
  {
    path: 'content',
    loadComponent: () => import('./features/content/content.page').then((m) => m.ContentPage)
  },
  {
    path: 'audience',
    loadComponent: () => import('./features/audience/audience.page').then((m) => m.AudiencePage)
  },
  {
    path: 'activity',
    loadComponent: () => import('./features/activity/activity.page').then((m) => m.ActivityPage)
  },
  {
    path: 'streams/current',
    loadComponent: () => import('./features/streams/current-stream.page').then((m) => m.CurrentStreamPage)
  },
  {
    path: 'alerts',
    loadComponent: () => import('./features/alerts/alerts.page').then((m) => m.AlertsPage)
  },
  {
    path: 'widgets',
    loadComponent: () => import('./features/widgets/widgets.page').then((m) => m.WidgetsPage)
  },
  {
    path: 'revenue',
    loadComponent: () => import('./features/revenue/revenue.page').then((m) => m.RevenuePage)
  },
  {
    path: 'polling',
    loadComponent: () => import('./features/polling/polling.page').then((m) => m.PollingPage)
  },
  {
    path: '**',
    redirectTo: 'overview'
  }
];
