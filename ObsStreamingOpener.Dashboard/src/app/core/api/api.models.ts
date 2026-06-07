export type ProviderKind = 'YouTube' | 'Tipply' | 'Patronite' | 'Zrzutka' | 'Custom';

export interface MonitoredAccountDto {
  id: string;
  displayName: string;
  isDefault: boolean;
}

export interface ConnectedAccountDto {
  accountId: string;
  displayName: string;
  provider: ProviderKind;
  externalAccountId: string | null;
  email: string | null;
  channelCount: number;
  isLoggedIn: boolean;
  hasRefreshToken: boolean;
  accessTokenExpiresAt: string | null;
  isExpired: boolean;
  lastRefreshedAt: string | null;
  disconnectedAt: string | null;
  scopes: string;
}

export interface YouTubeAuthorizationUrlDto {
  authorizationUrl: string;
}

export interface SaveAccountRequest {
  displayName: string;
  isDefault: boolean;
}

export interface MonitoredChannelDto {
  id: string;
  monitoredAccountId: string;
  provider: ProviderKind;
  externalChannelId: string;
  displayName: string;
  url: string | null;
  isDefault: boolean;
  isEnabled: boolean;
}

export interface SaveChannelSettingsRequest {
  displayName: string;
  url: string | null;
  isDefault: boolean;
  isEnabled: boolean;
}

export interface ProviderConnectionConfigDto {
  id: string;
  monitoredChannelId: string;
  provider: ProviderKind;
  externalChannelId: string;
  externalStreamId: string | null;
  displayName: string | null;
  isEnabled: boolean;
}

export interface SaveProviderConnectionRequest {
  monitoredChannelId: string;
  provider: ProviderKind;
  externalChannelId: string;
  externalStreamId: string | null;
  displayName: string | null;
  isEnabled: boolean;
}

export interface WidgetConfigurationDto {
  id: string;
  widgetKey: string;
  widgetType: string;
  theme: string;
  settingsJson: string;
  updatedAt: string;
}

export interface RevenueCurrencySummaryDto {
  currency: string;
  gross: number;
  knownNet: number;
  estimatedNet: number;
  platformFees: number;
  processorFees: number;
  payoutFees: number;
  positiveCount: number;
  negativeCount: number;
  pendingCount: number;
  settledCount: number;
  refundedOrReversedCount: number;
}

export interface RevenueSummaryDto {
  since: string | null;
  until: string | null;
  currencies: RevenueCurrencySummaryDto[];
}

export interface RevenueRankingEntryDto {
  supporterKey: string;
  displayName: string;
  currency: string;
  total: number;
  count: number;
}

export interface ForecastCurrencySummaryDto {
  currency: string;
  estimatedGross: number;
  activeSupportCount: number;
}

export interface ForecastSummaryDto {
  from: string;
  until: string;
  currencies: ForecastCurrencySummaryDto[];
}

export interface RevenueProviderStatusDto {
  provider: ProviderKind;
  enabled: boolean;
  status: string;
  lastSyncedAt: string | null;
  lastError: string | null;
}

export interface ProviderSyncResult {
  provider: ProviderKind;
  success: boolean;
  tipsProcessed: number;
  patronsProcessed: number;
  error: string | null;
}

export interface SaveWidgetConfigurationRequest {
  widgetKey: string;
  widgetType: string;
  theme: string;
  settingsJson: string;
}

export interface PollingConfigurationDto {
  enableStreamDataPolling: boolean;
  streamDataPollingSeconds: number;
  streamDataSchedule: string;
  accountDataSchedule: string;
}

export interface CurrentStatsDto {
  monitoredChannelId: string | null;
  concurrentViewers: number | null;
  likes: number | null;
  chatMessagesPerMinute: number | null;
  tipTotal: number;
  audienceMemberCount: number | null;
  paidAudienceMemberCount: number | null;
}

export interface StreamSessionDto {
  id: string;
  monitoredChannelId: string;
  title: string;
  isActive: boolean;
  startedAt: string;
  endedAt: string | null;
  provider: ProviderKind | null;
  externalSessionId: string | null;
  externalStreamId: string | null;
  externalLiveChatId: string | null;
  providerResourceId: string | null;
  scheduledStartAt: string | null;
  actualStartAt: string | null;
  actualEndAt: string | null;
}

export interface RecentEventDto {
  id: string;
  monitoredChannelId?: string;
  streamSessionId?: string | null;
  audienceMemberId?: string | null;
  providerResourceId?: string | null;
  provider?: ProviderKind;
  eventType?: string;
  actorName: string | null;
  title: string | null;
  message: string | null;
  amount: number | null;
  currency: string | null;
  occurredAt: string;
}

export interface ProviderResourceDto {
  id: string;
  monitoredChannelId: string;
  provider: ProviderKind;
  resourceKind: 'Channel' | 'Video' | 'LiveBroadcast' | 'LiveStream' | 'UploadPlaylistItem' | string;
  observedKinds: string[];
  externalResourceId: string;
  title: string | null;
  description: string | null;
  url: string | null;
  status: string | null;
  publishedAt: string | null;
  scheduledStartAt: string | null;
  actualStartAt: string | null;
  actualEndAt: string | null;
  lastSyncedAt: string;
  patchHistory: ProviderResourcePatchDto[];
}

export interface ProviderResourcePatchDto {
  capturedAtUtc: string;
  source: string;
  fields: ProviderResourcePatchFieldDto[];
}

export interface ProviderResourcePatchFieldDto {
  field: string;
  oldValue: string | null;
  newValue: string | null;
}

export interface ProviderMessageDto {
  id: string;
  monitoredChannelId: string;
  streamSessionId: string | null;
  providerResourceId: string | null;
  provider: ProviderKind;
  source: 'LiveChat' | 'VideoComment' | 'CommentReply' | string;
  externalMessageId: string | null;
  identityKey: string;
  authorExternalId: string | null;
  authorDisplayName: string | null;
  authorProfileImageUrl: string | null;
  messageText: string | null;
  publishedAt: string;
  likeCount: number | null;
  isOwner: boolean;
  isModerator: boolean;
  isVerified: boolean;
  isSponsor: boolean;
  amount: number | null;
  currency: string | null;
  lastSeenAt: string;
}

export interface AudienceCurrencyTotalDto {
  currency: string;
  total: number;
}

export interface AudienceRelationshipPeriodDto {
  id: string;
  monitoredChannelId: string;
  audienceMemberId: string;
  relationshipKind: 'Free' | 'Paid' | string;
  startedAt: string;
  endedAt: string | null;
  isEstimated: boolean;
  audienceDisplayName: string | null;
  isPatron: boolean;
  patronTierName: string | null;
  latestCurrencyTotal: number | null;
  latestCurrency: string | null;
  currencyTotals: AudienceCurrencyTotalDto[] | null;
  lastActivityAt: string | null;
}

export interface AudienceActivityDto {
  audienceMemberId: string;
  relationships: AudienceRelationshipPeriodDto[];
  events: RecentEventDto[];
  messages: ProviderMessageDto[];
  revenue: {
    latestCurrencyTotal: number | null;
    latestCurrency: string | null;
    currencyTotals: AudienceCurrencyTotalDto[];
  };
}

export interface StreamAlertDto {
  id: string;
  monitoredChannelId: string;
  streamSessionId: string;
  streamEventId: string | null;
  alertType: string;
  provider: ProviderKind;
  isSystemAlert: boolean;
  title: string;
  message: string | null;
  actorName: string | null;
  amount: number | null;
  currency: string | null;
  visualStyle: string;
  mediaUrl: string | null;
  soundUrl: string | null;
  displayFromUtc: string;
  displayUntilUtc: string;
  acknowledgedAtUtc: string | null;
  createdAtUtc: string;
  sourceEventType: string | null;
  sourceEventTitle: string | null;
  sourceEventMessage: string | null;
  sourceEventOccurredAt: string | null;
}

export interface StreamEventAlertTraceDto {
  eventId: string;
  monitoredChannelId: string;
  streamSessionId: string | null;
  provider: ProviderKind;
  eventType: string;
  actorName: string | null;
  title: string | null;
  message: string | null;
  amount: number | null;
  currency: string | null;
  occurredAt: string;
  alertId: string | null;
  alertStatus: string;
}

export interface ManualAlertRequest {
  streamSessionId: string | null;
  title: string;
  message: string | null;
  visualStyle: string | null;
  durationSeconds: number | null;
  mediaUrl: string | null;
  soundUrl: string | null;
}

export interface AlertRuleDto {
  id: string;
  monitoredChannelId: string;
  eventType: string;
  enabled: boolean;
  minimumAmount: number | null;
  durationSeconds: number;
  visualStyle: string;
  titleTemplate: string | null;
  messageTemplate: string | null;
  mediaUrl: string | null;
  soundUrl: string | null;
  updatedAtUtc: string;
}
