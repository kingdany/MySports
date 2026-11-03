import { SymbolView } from 'expo-symbols';
import { Tabs } from 'expo-router';

import Colors from '@/constants/Colors';
import { useColorScheme } from '@/components/useColorScheme';
import { useClientOnlyValue } from '@/components/useClientOnlyValue';

export default function TabLayout() {
  const colorScheme = useColorScheme();

  return (
    <Tabs
      screenOptions={{
        tabBarActiveTintColor: Colors[colorScheme].tint,
        // Disable the static render of the header on web
        // to prevent a hydration error in React Navigation v6.
        headerShown: useClientOnlyValue(false, true),
      }}>
      <Tabs.Screen
        name="index"
        options={{
          title: 'Prossimi',
          tabBarIcon: ({ color }) => (
            <SymbolView
              name={{ ios: 'calendar', android: 'calendar_month', web: 'calendar_month' }}
              tintColor={color}
              size={28}
            />
          ),
        }}
      />
      <Tabs.Screen
        name="results"
        options={{
          title: 'Risultati',
          tabBarIcon: ({ color }) => (
            <SymbolView
              name={{ ios: 'trophy', android: 'emoji_events', web: 'emoji_events' }}
              tintColor={color}
              size={28}
            />
          ),
        }}
      />
      <Tabs.Screen
        name="standings"
        options={{
          title: 'Classifiche',
          tabBarIcon: ({ color }) => (
            <SymbolView
              name={{ ios: 'list.number', android: 'leaderboard', web: 'leaderboard' }}
              tintColor={color}
              size={28}
            />
          ),
        }}
      />
    </Tabs>
  );
}
