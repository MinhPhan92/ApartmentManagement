export interface ResidentApartmentOption {
  apartmentId: number;
  apartmentCode: string;
  buildingName: string;
  label: string;
}

export interface ResidentActiveApartment {
  apartmentId: number;
  apartmentCode: string;
  buildingId: number;
  buildingCode: string;
  buildingName: string;
  buildingAddress?: string;
  floor: number;
  area: number;
  status: string;
  relationship: string;
  isOwner: boolean;
  moveInDate: string;
}

export interface ResidentInvoiceSummary {
  apartmentInvoiceId: number;
  invoiceCode: string;
  apartmentId: number;
  apartmentCode: string;
  buildingName: string;
  billingYear: number;
  billingMonth: number;
  periodLabel: string;
  dueDate: string;
  totalAmount: number;
  confirmedAmount: number;
  remainingAmount: number;
  status: string;
  paymentState: 'Paid' | 'Partial' | 'Overdue' | 'Unpaid' | 'Void' | string;
  hasPendingTransaction: boolean;
  issuedAtUtc: string | null;
}

export interface ResidentInvoiceLine {
  lineId: number;
  chargeType: string;
  chargeTypeLabel: string;
  description: string;
  quantity: number;
  unitRate: number;
  amount: number;
  previousReading: number | null;
  currentReading: number | null;
  rateBreakdown: string | null;
}

export interface ResidentPaymentTransaction {
  paymentTransactionId: number;
  reference: string;
  amount: number;
  status: 'Pending' | 'Confirmed' | 'Rejected' | string;
  createdAtUtc: string;
  confirmedAtUtc: string | null;
  rejectedAtUtc: string | null;
  rejectionReason: string | null;
  isAutoClosedRejection: boolean;
  qrImageUrl: string | null;
}

export interface ResidentInvoiceDetailsResponse {
  summary: ResidentInvoiceSummary;
  apartmentArea: number;
  lines: ResidentInvoiceLine[];
  transactions: ResidentPaymentTransaction[];
  activePayment: {
    paymentTransactionId: number;
    reference: string;
    amount: number;
    status: string;
    createdAtUtc: string;
    qrImageUrl: string | null;
  } | null;
  vietQr: {
    configured: boolean;
    amountSupported: boolean;
    canCreatePaymentAttempt: boolean;
    bankBin: string;
    accountNumber: string;
    accountName: string;
  };
}

export interface ResidentInvoicesListResponse {
  totalCount: number;
  totalBilledAmount: number;
  totalConfirmedAmount: number;
  totalRemainingAmount: number;
  vietQrConfigured: boolean;
  items: ResidentInvoiceSummary[];
}

export interface ResidentParkingCard {
  parkingCardId: number;
  cardCode: string;
  status: 'Active' | 'Lost' | 'Locked' | string;
  issuedAtUtc: string;
}

export interface ResidentVehicle {
  vehicleId: number;
  apartmentId: number;
  apartmentCode: string;
  vehicleType: string;
  licensePlate: string;
  brandModel: string | null;
  color: string | null;
  status: string;
  createdAt: string;
  reviewedAt: string | null;
  reviewNotes: string | null;
  parkingCard: ResidentParkingCard | null;
}

export interface ResidentVisitor {
  visitorPassId: number;
  apartmentId: number;
  apartmentCode: string;
  visitorName: string;
  phoneNumber: string | null;
  idCardNumber: string | null;
  expectedArrivalDate: string;
  actualCheckInUtc: string | null;
  actualCheckOutUtc: string | null;
  status: string;
  purpose: string | null;
  createdAtUtc: string;
  canCancel: boolean;
}

export interface ResidentRepairSummary {
  repairRequestId: number;
  apartmentId: number;
  apartmentCode: string;
  buildingName: string;
  title: string;
  description: string;
  priority: string;
  status: string;
  result: string | null;
  createdAt: string;
  completedAt: string | null;
  confirmedAt: string | null;
  rating: number | null;
  residentFeedback: string | null;
  rowVersionBase64: string;
}

export interface ResidentRepairAssignment {
  assignmentId: number;
  technicianName: string;
  assignedAt: string;
  completedAt: string | null;
  status: string;
  note: string | null;
}

export interface ResidentRepairStatusHistory {
  historyId: number;
  previousStatus: string | null;
  newStatus: string;
  changedAt: string;
  changedByName: string;
  note: string | null;
}

export interface ResidentRepairDetails extends ResidentRepairSummary {
  assignments: ResidentRepairAssignment[];
  statusHistory: ResidentRepairStatusHistory[];
}

export interface ResidentAmenity {
  amenityId: number;
  name: string;
  description: string | null;
  location: string | null;
  capacity: number;
  openTime: string;
  closeTime: string;
  isActive: boolean;
  pricePerHour: number;
  isFree: boolean;
}

export interface ResidentAmenityBooking {
  amenityBookingId: number;
  amenityId: number;
  amenityName: string;
  amenityLocation: string;
  bookingDate: string;
  startTime: string;
  endTime: string;
  numberOfGuests: number;
  status: string;
  notes: string | null;
  estimatedCost: number;
  createdAtUtc: string;
  canCancel: boolean;
}

export interface AmenityBookedSlot {
  bookingId: number;
  startTime: string;
  endTime: string;
  guests: number;
  status: string;
}

export interface ResidentComplaint {
  complaintId: number;
  apartmentId: number | null;
  apartmentCode: string | null;
  buildingName: string | null;
  title: string;
  content: string;
  category: string;
  priority: string;
  status: string;
  createdAt: string;
  resolvedAt: string | null;
  responseNotes: string | null;
  rating: number | null;
  residentFeedback: string | null;
  ratedAt: string | null;
  canEdit: boolean;
  canCancel: boolean;
  canRate: boolean;
}

export interface ResidentAnnouncement {
  announcementId: number;
  title: string;
  content: string;
  category: string;
  isPinned: boolean;
  buildingId: number | null;
  buildingName: string;
  createdAtUtc: string;
  displayDate: string;
}

export interface ResidentNotificationItem {
  id: number;
  title: string;
  content: string;
  type: string;
  isRead: boolean;
  targetUrl: string | null;
  createdAtUtc: string;
  displayDate: string;
}

export interface ResidentDashboardResponse {
  userFullName: string;
  userEmail: string;
  residentId: number | null;
  activeApartment: ResidentActiveApartment | null;
  householdCount: number;
  totalOutstandingDebt: number;
  unpaidInvoiceCount: number;
  latestInvoice: ResidentInvoiceSummary | null;
  activeRepairsCount: number;
  registeredVehiclesCount: number;
  activeParkingCardsCount: number;
  unreadNotificationsCount: number;
  recentRepairs: ResidentRepairSummary[];
  vehicles: ResidentVehicle[];
  recentVisitors: ResidentVisitor[];
  recentAnnouncements: ResidentAnnouncement[];
  recentNotifications: ResidentNotificationItem[];
}

export interface ResidentHouseholdMember {
  apartmentResidentId: number;
  residentId: number;
  fullName: string;
  email: string;
  relationship: string;
  isOwner: boolean;
  moveInDate: string;
}

export interface ResidentOccupancyHistory {
  apartmentResidentId: number;
  apartmentId: number;
  apartmentCode: string;
  buildingName: string;
  floor: number;
  area: number;
  relationship: string;
  isOwner: boolean;
  moveInDate: string;
  moveOutDate: string | null;
  isActive: boolean;
}

export interface ResidentContractItem {
  contractId: number;
  contractCode: string;
  apartmentId: number;
  apartmentCode: string;
  buildingName: string;
  startDate: string;
  endDate: string;
  monthlyRent: number;
  depositAmount: number;
  status: string;
  termsSummary: string | null;
  parties: Array<{
    residentId: number;
    fullName: string;
    role: string;
  }>;
  statusHistory: Array<{
    previousStatus: string | null;
    newStatus: string;
    changedAtUtc: string;
    reason: string | null;
  }>;
}

export interface ResidentApartmentResponse {
  hasResidentProfile: boolean;
  activeApartment: ResidentActiveApartment | null;
  householdMembers: ResidentHouseholdMember[];
  occupancyHistory: ResidentOccupancyHistory[];
  contracts: ResidentContractItem[];
}

export interface ResidentProfileResponse {
  residentId: number;
  fullName: string;
  email: string;
  userName: string;
  phoneNumber: string;
  citizenId: string;
  dateOfBirth: string | null;
  gender: string;
  address: string;
  emergencyContact: string;
}

