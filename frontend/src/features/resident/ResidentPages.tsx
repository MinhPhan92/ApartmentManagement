import { ResidentDashboardPage } from './ResidentDashboardPage';
import { ResidentApartmentPage } from './ResidentApartmentPage';
import { ResidentInvoicesPage } from './ResidentInvoicesPage';
import { ResidentRepairsPage } from './ResidentRepairsPage';
import { ResidentComplaintsPage } from './ResidentComplaintsPage';
import { ResidentVehiclesPage } from './ResidentVehiclesPage';
import { ResidentVisitorsPage } from './ResidentVisitorsPage';
import { ResidentAmenitiesPage } from './ResidentAmenitiesPage';
import { ResidentNotificationsPage } from './ResidentNotificationsPage';
import { ResidentAccountPage } from './ResidentAccountPage';

export function ResidentRoutePage({ itemId }: { itemId: string }) {
  switch (itemId) {
    case 'resident-dashboard':
      return <ResidentDashboardPage />;
    case 'resident-apartment':
      return <ResidentApartmentPage />;
    case 'resident-invoices':
      return <ResidentInvoicesPage />;
    case 'resident-repairs':
      return <ResidentRepairsPage />;
    case 'resident-complaints':
      return <ResidentComplaintsPage />;
    case 'resident-vehicles':
      return <ResidentVehiclesPage />;
    case 'resident-visitors':
      return <ResidentVisitorsPage />;
    case 'resident-amenities':
      return <ResidentAmenitiesPage />;
    case 'resident-notifications':
      return <ResidentNotificationsPage />;
    case 'resident-account':
      return <ResidentAccountPage />;
    default:
      return <ResidentDashboardPage />;
  }
}
