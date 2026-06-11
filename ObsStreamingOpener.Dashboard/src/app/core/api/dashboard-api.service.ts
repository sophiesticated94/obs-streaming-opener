import { HttpClient } from '@angular/common/http';
import { Injectable } from '@angular/core';
import {
  ConnectedAccountDto,
  CurrentStatsDto,
  AlertWidgetSettingsDto,
  AlertRuleDto,
  ManualAlertRequest,
  MonitoredAccountDto,
  MonitoredChannelDto,
  PollingConfigurationDto,
  ProviderMessageDto,
  ProviderConnectionConfigDto,
  ProviderResourceDto,
  ForecastSummaryDto,
  RecentEventDto,
  RevenueProviderStatusDto,
  RevenueRankingEntryDto,
  RevenueSummaryDto,
  ProviderSyncResult,
  StreamAlertDto,
  StreamEventAlertTraceDto,
  StreamSessionDto,
  SaveAccountRequest,
  SaveAlertRuleRequest,
  SaveChannelSettingsRequest,
  SaveProviderConnectionRequest,
  SaveWidgetConfigurationRequest,
  StatsSummaryDto,
  WidgetConfigurationDto,
  YouTubeAuthorizationUrlDto,
  AudienceActivityDto,
  AudienceRelationshipPeriodDto
} from './api.models';

@Injectable({ providedIn: 'root' })
export class DashboardApiService {
  constructor(private readonly http: HttpClient) {}

  getAccounts() {
    return this.http.get<MonitoredAccountDto[]>('/api/config/accounts');
  }

  getConnectedAccounts() {
    return this.http.get<ConnectedAccountDto[]>('/api/config/accounts/connected');
  }

  createAccount(request: SaveAccountRequest) {
    return this.http.post<MonitoredAccountDto>('/api/config/accounts', request);
  }

  updateAccount(id: string, request: SaveAccountRequest) {
    return this.http.put<MonitoredAccountDto>(`/api/config/accounts/${id}`, request);
  }

  getChannels() {
    return this.http.get<MonitoredChannelDto[]>('/api/config/channels');
  }

  updateChannel(id: string, request: SaveChannelSettingsRequest) {
    return this.http.put<MonitoredChannelDto>(`/api/config/channels/${id}`, request);
  }

  getProviderConnections(channelId?: string) {
    const query = channelId ? `?channelId=${channelId}` : '';
    return this.http.get<ProviderConnectionConfigDto[]>(`/api/config/provider-connections${query}`);
  }

  createProviderConnection(request: SaveProviderConnectionRequest) {
    return this.http.post<ProviderConnectionConfigDto>('/api/config/provider-connections', request);
  }

  updateProviderConnection(id: string, request: SaveProviderConnectionRequest) {
    return this.http.put<ProviderConnectionConfigDto>(`/api/config/provider-connections/${id}`, request);
  }

  deleteProviderConnection(id: string) {
    return this.http.delete<void>(`/api/config/provider-connections/${id}`);
  }

  getWidgets() {
    return this.http.get<WidgetConfigurationDto[]>('/api/config/widgets');
  }

  upsertWidget(request: SaveWidgetConfigurationRequest) {
    return this.http.put<WidgetConfigurationDto>('/api/config/widgets', request);
  }

  getAlertWidgetSettings() {
    return this.http.get<AlertWidgetSettingsDto>('/api/config/widgets/alerts');
  }

  upsertAlertWidgetSettings(request: AlertWidgetSettingsDto) {
    return this.http.put<AlertWidgetSettingsDto>('/api/config/widgets/alerts', request);
  }

  getPolling() {
    return this.http.get<PollingConfigurationDto>('/api/config/polling');
  }

  startYouTubeLogin(accountId?: string) {
    const query = accountId ? `?accountId=${accountId}` : '';
    return this.http.get<YouTubeAuthorizationUrlDto>(`/api/auth/youtube/start${query}`);
  }

  reloginYouTube(accountId: string) {
    return this.http.post<YouTubeAuthorizationUrlDto>(`/api/auth/youtube/relogin/${accountId}`, {});
  }

  refreshYouTubeToken(accountId: string) {
    return this.http.post<ConnectedAccountDto>(`/api/auth/youtube/refresh/${accountId}`, {});
  }

  syncYouTubeAccount(accountId: string) {
    return this.http.post<ConnectedAccountDto>(`/api/auth/youtube/sync/${accountId}`, {});
  }

  disconnectYouTube(accountId: string) {
    return this.http.delete<void>(`/api/auth/youtube/disconnect/${accountId}`);
  }

  getCurrentStats() {
    return this.http.get<CurrentStatsDto>('/api/stats/current');
  }

  getRecentEvents() {
    return this.http.get<RecentEventDto[]>('/api/events/recent?limit=8');
  }

  getCurrentStream(channelId: string) {
    return this.http.get<StreamSessionDto | null>(`/api/channels/${channelId}/stream/current`);
  }

  getChannelStats(channelId: string, filters: DashboardFilters = {}) {
    const params = this.toParams({ ...filters });
    const query = params ? `?${params}` : '';
    return this.http.get<CurrentStatsDto>(`/api/channels/${channelId}/stats/current${query}`);
  }

  getChannelStatsSummary(channelId: string, filters: DashboardFilters = {}) {
    const params = this.toParams({ ...filters });
    const query = params ? `?${params}` : '';
    return this.http.get<StatsSummaryDto>(`/api/channels/${channelId}/stats/summary${query}`);
  }

  getChannelRecentEvents(channelId: string, limit = 12, filters: DashboardFilters = {}) {
    const params = this.toParams({ limit, ...filters });
    return this.http.get<RecentEventDto[]>(`/api/channels/${channelId}/events/recent?${params}`);
  }

  getChannelRecentContent(channelId: string, limit = 20) {
    return this.http.get<ProviderResourceDto[]>(`/api/channels/${channelId}/content/recent?limit=${limit}`);
  }

  getChannelContent(channelId: string, resourceId: string) {
    return this.http.get<ProviderResourceDto>(`/api/channels/${channelId}/content/${resourceId}`);
  }

  getChannelUpcomingContent(channelId: string, limit = 10) {
    return this.http.get<ProviderResourceDto[]>(`/api/channels/${channelId}/content/upcoming?limit=${limit}`);
  }

  getChannelRecentMessages(channelId: string, limit = 10, filters: DashboardFilters = {}) {
    const params = this.toParams({ limit, ...filters });
    return this.http.get<ProviderMessageDto[]>(`/api/channels/${channelId}/messages/recent?${params}`);
  }

  searchChannelMessages(channelId: string, query: string, limit = 30) {
    const params = new URLSearchParams({ query, limit: String(limit) });
    return this.http.get<ProviderMessageDto[]>(`/api/channels/${channelId}/messages/search?${params}`);
  }

  getActiveAlerts(channelId: string, streamSessionId?: string) {
    const query = streamSessionId ? `?streamSessionId=${streamSessionId}` : '';
    return this.http.get<StreamAlertDto[]>(`/api/channels/${channelId}/alerts/active${query}`);
  }

  getRecentAlerts(channelId: string, streamSessionId?: string, limit = 20) {
    const params = new URLSearchParams({ limit: String(limit) });
    if (streamSessionId) {
      params.set('streamSessionId', streamSessionId);
    }

    return this.http.get<StreamAlertDto[]>(`/api/channels/${channelId}/alerts/recent?${params}`);
  }

  getEventAlertTrace(channelId: string, streamSessionId?: string, limit = 50, filters: DashboardFilters = {}) {
    const params = new URLSearchParams({ limit: String(limit) });
    if (streamSessionId) {
      params.set('streamSessionId', streamSessionId);
    }
    for (const [key, value] of Object.entries(filters)) {
      if (value !== null && value !== undefined && value !== '') {
        params.set(key, String(value));
      }
    }

    return this.http.get<StreamEventAlertTraceDto[]>(`/api/channels/${channelId}/events/alert-trace?${params}`);
  }

  getRecentAudience(channelId: string, limit = 50, includeRevenue = true) {
    return this.http.get<AudienceRelationshipPeriodDto[]>(`/api/channels/${channelId}/audience/recent?limit=${limit}&includeRevenue=${includeRevenue}`);
  }

  getAudienceActivity(channelId: string, audienceMemberId: string) {
    return this.http.get<AudienceActivityDto>(`/api/channels/${channelId}/audience/${audienceMemberId}/activity`);
  }

  createManualAlert(channelId: string, request: ManualAlertRequest) {
    return this.http.post<StreamAlertDto>(`/api/channels/${channelId}/alerts/manual`, request);
  }

  acknowledgeAlert(channelId: string, alertId: string) {
    return this.http.post<void>(`/api/channels/${channelId}/alerts/${alertId}/ack`, {});
  }

  getAlertRules(channelId?: string) {
    const query = channelId ? `?channelId=${channelId}` : '';
    return this.http.get<AlertRuleDto[]>(`/api/config/alert-rules${query}`);
  }

  upsertAlertRule(request: SaveAlertRuleRequest) {
    return this.http.put<AlertRuleDto>('/api/config/alert-rules', request);
  }

  getRevenueSummary() {
    return this.http.get<RevenueSummaryDto>('/api/revenue/summary');
  }

  getRevenueRanking() {
    return this.http.get<RevenueRankingEntryDto[]>('/api/revenue/rankings');
  }

  getRevenueForecast(days = 30) {
    return this.http.get<ForecastSummaryDto>(`/api/revenue/forecast?days=${days}`);
  }

  getRevenueProviderStatus() {
    return this.http.get<RevenueProviderStatusDto[]>('/api/revenue/providers/status');
  }

  syncRevenue() {
    return this.http.post<ProviderSyncResult[]>('/api/revenue/sync', {});
  }

  private toParams(values: Record<string, string | number | null | undefined>) {
    const params = new URLSearchParams();
    for (const [key, value] of Object.entries(values)) {
      if (value !== null && value !== undefined && value !== '') {
        params.set(key, String(value));
      }
    }

    return params;
  }
}

export interface DashboardFilters {
  providerResourceId?: string | null;
  streamSessionId?: string | null;
  audienceMemberId?: string | null;
}
