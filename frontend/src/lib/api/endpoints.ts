export const API_ENDPOINTS = {
  auth: {
    csrf: '/api/auth/csrf',
    me: '/api/auth/me',
    login: '/api/auth/login',
    logout: '/api/auth/logout',
  },
  notifications: {
    recent: '/api/notifications/recent',
    unreadCount: '/api/notifications/unread-count',
    markRead: (id: number) => `/api/notifications/mark-read/${id}`,
    markAllRead: '/api/notifications/mark-all-read',
  },
  resident: {
    dashboard: '/api/resident/dashboard',
    apartment: '/api/resident/apartment',
    invoices: '/api/resident/invoices',
    invoiceDetails: (id: number) => `/api/resident/invoices/${id}`,
    createPaymentAttempt: (id: number) => `/api/resident/invoices/${id}/payment-attempts`,
    transactionQr: (id: number) => `/api/resident/invoices/transactions/${id}/qr`,
    amenities: '/api/resident/amenities',
    amenityBookedSlots: (id: number, date: string) =>
      `/api/resident/amenities/${id}/booked-slots?date=${encodeURIComponent(date)}`,
    createAmenityBooking: '/api/resident/amenities/bookings',
    cancelAmenityBooking: (id: number) => `/api/resident/amenities/bookings/${id}/cancel`,
    vehicles: '/api/resident/vehicles',
    cancelVehicle: (id: number) => `/api/resident/vehicles/${id}/cancel`,
    lockVehicleCard: (id: number) => `/api/resident/vehicles/${id}/lock-card`,
    decommissionVehicle: (id: number) => `/api/resident/vehicles/${id}/decommission`,
    visitors: '/api/resident/visitors',
    cancelVisitor: (id: number) => `/api/resident/visitors/${id}/cancel`,
    repairs: '/api/resident/repairs',
    repairDetails: (id: number) => `/api/resident/repairs/${id}`,
    cancelRepair: (id: number) => `/api/resident/repairs/${id}/cancel`,
    confirmRepair: (id: number) => `/api/resident/repairs/${id}/confirm`,
    redoRepair: (id: number) => `/api/resident/repairs/${id}/request-redo`,
    complaints: '/api/resident/complaints',
    complaintDetails: (id: number) => `/api/resident/complaints/${id}`,
    cancelComplaint: (id: number) => `/api/resident/complaints/${id}/cancel`,
    rateComplaint: (id: number) => `/api/resident/complaints/${id}/rate`,
    notifications: (page = 1, unreadOnly?: boolean) =>
      `/api/resident/notifications?page=${page}${
        unreadOnly !== undefined ? `&unreadOnly=${unreadOnly}` : ''
      }`,
    deleteNotification: (id: number) => `/api/resident/notifications/${id}`,
    profile: '/api/resident/profile',
    changePassword: '/api/resident/change-password',
  },
  search: {
    global: (query: string) => `/api/search?q=${encodeURIComponent(query)}`,
  },
  hubs: {
    notifications: '/hubs/notifications',
  },
} as const;
