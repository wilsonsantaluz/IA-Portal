CREATE TABLE ChannelConnections (
 Id TEXT NOT NULL PRIMARY KEY, OrganizationId INTEGER NOT NULL REFERENCES Organizations(Id),
 Provider TEXT NOT NULL, ExternalAccountId TEXT NOT NULL, ExternalPhoneNumberId TEXT NOT NULL,
 DisplayName TEXT NOT NULL, IsEnabled INTEGER NOT NULL, AutoReply INTEGER NOT NULL,
 AgentEnabled INTEGER NOT NULL, AgentId TEXT NOT NULL, KnowledgeIdsJson TEXT NOT NULL,
 RetentionDays INTEGER NOT NULL, MonthlyLimit INTEGER NOT NULL, Version INTEGER NOT NULL,
 UNIQUE(OrganizationId,Id)
);
CREATE UNIQUE INDEX IX_ChannelConnections_Provider_ExternalAccountId_ExternalPhoneNumberId ON ChannelConnections(Provider,ExternalAccountId,ExternalPhoneNumberId);
CREATE TABLE ChannelContacts (
 Id TEXT NOT NULL PRIMARY KEY, OrganizationId INTEGER NOT NULL, ConnectionId TEXT NOT NULL, ExternalId TEXT NOT NULL,
 UNIQUE(OrganizationId,ConnectionId,Id), FOREIGN KEY(OrganizationId,ConnectionId) REFERENCES ChannelConnections(OrganizationId,Id)
);
CREATE UNIQUE INDEX IX_ChannelContacts_ConnectionId_ExternalId ON ChannelContacts(ConnectionId,ExternalId);
CREATE TABLE ChannelConversations (
 Id TEXT NOT NULL PRIMARY KEY, OrganizationId INTEGER NOT NULL, ConnectionId TEXT NOT NULL, ContactId TEXT NOT NULL,
 HumanHandoff INTEGER NOT NULL, LastInboundAt TEXT NOT NULL, UpdatedAt TEXT NOT NULL, Version INTEGER NOT NULL,
 UNIQUE(OrganizationId,ConnectionId,Id), FOREIGN KEY(OrganizationId,ConnectionId,ContactId) REFERENCES ChannelContacts(OrganizationId,ConnectionId,Id)
);
CREATE UNIQUE INDEX IX_ChannelConversations_ConnectionId_ContactId ON ChannelConversations(ConnectionId,ContactId);
CREATE TABLE ChannelMessages (
 Id TEXT NOT NULL PRIMARY KEY, OrganizationId INTEGER NOT NULL, ConnectionId TEXT NOT NULL, ConversationId TEXT NOT NULL,
 ExternalId TEXT NOT NULL, Direction TEXT NOT NULL, Text TEXT NOT NULL, State TEXT NOT NULL,
 CreatedAt TEXT NOT NULL, NextAttemptAt TEXT NOT NULL, Attempts INTEGER NOT NULL, SourcesJson TEXT NOT NULL, Category TEXT NOT NULL,
 UNIQUE(OrganizationId,Id), FOREIGN KEY(OrganizationId,ConnectionId,ConversationId) REFERENCES ChannelConversations(OrganizationId,ConnectionId,Id)
);
CREATE UNIQUE INDEX IX_ChannelMessages_ConnectionId_ExternalId ON ChannelMessages(ConnectionId,ExternalId);
CREATE TABLE MessageDeliveryEvents (
 Id TEXT NOT NULL PRIMARY KEY, OrganizationId INTEGER NOT NULL, MessageId TEXT NOT NULL, ExternalKey TEXT NOT NULL,
 Status TEXT NOT NULL, PricingCategory TEXT NULL, Billable INTEGER NULL, CreatedAt TEXT NOT NULL,
 FOREIGN KEY(OrganizationId,MessageId) REFERENCES ChannelMessages(OrganizationId,Id)
);
CREATE UNIQUE INDEX IX_MessageDeliveryEvents_MessageId_ExternalKey ON MessageDeliveryEvents(MessageId,ExternalKey);
